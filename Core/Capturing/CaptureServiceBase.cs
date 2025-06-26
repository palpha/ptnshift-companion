using System.Diagnostics.CodeAnalysis;
using Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public abstract class CaptureServiceBase : ICaptureService
{
    protected CaptureServiceBase(
        IStreamer streamer,
        IDisplayService displayService,
        IPtnshiftFinder ptnshiftFinder,
        IDiagnosticOutputRenderer diagnosticOutputRenderer,
        ILogger<CaptureServiceBase> logger,
        TimeProvider timeProvider)
    {
        Streamer = streamer;
        DisplayService = displayService;
        PtnshiftFinder = ptnshiftFinder;
        DiagnosticOutputRenderer = diagnosticOutputRenderer;
        Logger = logger;
        TimeProvider = timeProvider;

        StartCaptureMonitoring();
    }

    protected IStreamer Streamer { get; }

    private IDisplayService DisplayService { get; }
    private IPtnshiftFinder PtnshiftFinder { get; }
    private IDiagnosticOutputRenderer DiagnosticOutputRenderer { get; }

    private int DisplayWidth { get; set; }
    private ITimer CaptureMonitoringTimer { get; set; }

    protected ILogger<CaptureServiceBase> Logger { get; }
    private TimeProvider TimeProvider { get; }

    protected CaptureConfiguration? CurrentConfiguration { get; private set; }

    public event Action<ReadOnlySpan<byte>> FrameCaptured = delegate { };

    public bool IsCapturing => Streamer.IsCapturing;

    public abstract Task<bool> CheckCapturePermissionAsync();
    public abstract int GetConfigurationChangeDelayMs(CaptureConfiguration configuration);

    public virtual void SetConfiguration(CaptureConfiguration configuration)
    {
        Logger.LogInformation("Setting configuration: {Configuration}", configuration);

        var previousConfiguration = CurrentConfiguration;
        CurrentConfiguration = configuration;

        if (configuration.DisplayId.HasValue)
        {
            var display = DisplayService.GetDisplay(configuration.DisplayId);
            DisplayWidth = display?.Width ?? 0;
        }

        if (Streamer.IsCapturing && previousConfiguration != null && previousConfiguration != CurrentConfiguration)
        {
            // Use fire-and-forget for the async configuration update
            _ = Task.Run(async () =>
            {
                try
                {
                    await UpdateStreamerConfigurationAsync(previousConfiguration);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to update streamer configuration");
                }
            });
        }

        Logger.LogInformation("Configuration set");
    }

    public virtual void StartCapture()
    {
        Logger.LogInformation("Starting capture");

        if (CurrentConfiguration == null)
        {
            Logger.LogWarning("No current configuration available");
            throw new InvalidOperationException("Configuration not set.");
        }

        if (IsCapturing)
        {
            Logger.LogWarning("Already capturing");
            return;
        }

        if (CurrentConfiguration.IsValid(DisplayService.AvailableDisplays) == false)
        {
            Logger.LogWarning("Invalid capture configuration");
            return;
        }

        Streamer.EventSource.RegionFrameCaptured += OnRegionFrameReceived;
        Streamer.EventSource.FullScreenFrameCaptured += OnFullScreenFrameReceived;
        Streamer.EventSource.RegionCaptureStopped += OnCaptureStopped;
        Streamer.EventSource.FullScreenCaptureStopped += OnCaptureStopped;
        StartStreamer();

        Logger.LogInformation("Started capture");
    }

    public async Task StopCaptureAsync()
    {
        Logger.LogInformation("Stopping capture");

        if (IsCapturing == false)
        {
            Logger.LogInformation("Was not capturing");

            return;
        }

        Streamer.EventSource.RegionFrameCaptured -= OnRegionFrameReceived;
        Streamer.EventSource.FullScreenFrameCaptured -= OnFullScreenFrameReceived;
        Streamer.EventSource.RegionCaptureStopped -= OnCaptureStopped;
        Streamer.EventSource.FullScreenCaptureStopped -= OnCaptureStopped;
        await StopStreamerAsync();

        Logger.LogInformation("Stopped capture");
    }

    /// <summary>
    /// Synchronous stop method for disposal scenarios where async is not possible.
    /// </summary>
    private void StopCaptureInternal()
    {
        Logger.LogInformation("Stopping capture (internal)");

        if (IsCapturing == false)
        {
            Logger.LogInformation("Was not capturing");
            return;
        }

        Streamer.EventSource.RegionFrameCaptured -= OnRegionFrameReceived;
        Streamer.EventSource.FullScreenFrameCaptured -= OnFullScreenFrameReceived;
        Streamer.EventSource.RegionCaptureStopped -= OnCaptureStopped;
        Streamer.EventSource.FullScreenCaptureStopped -= OnCaptureStopped;

        // Use a synchronous wait for the async stop operation in disposal scenarios
        try
        {
            StopStreamerAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during internal stop");
        }

        Logger.LogInformation("Stopped capture");
    }

    protected async Task StopStreamerAsync()
    {
        await Streamer.StopAsync();
        DiagnosticOutputRenderer.SetText(Subsystem.PixelCapture, "Not capturing");
    }

    protected void StartStreamer()
    {
        if (CurrentConfiguration == null || CurrentConfiguration.DisplayId.HasValue == false)
        {
            throw new InvalidOperationException("Configuration/display not set.");
        }

        try
        {
            Streamer.Start(
                CurrentConfiguration.DisplayId.Value,
                CurrentConfiguration.CaptureX,
                CurrentConfiguration.CaptureY,
                CurrentConfiguration.Width,
                CurrentConfiguration.Height,
                CurrentConfiguration.FrameRate);

            DiagnosticOutputRenderer.SetText(Subsystem.PixelCapture, "Capturing");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to start capture");
            throw;
        }
    }

    [MemberNotNull(nameof(CaptureMonitoringTimer))]
    private void StartCaptureMonitoring()
    {
        CaptureMonitoringTimer = TimeProvider.CreateTimer(_ =>
        {
            var label = IsCapturing ? "Capturing" : "Not capturing";
            DiagnosticOutputRenderer.SetText(Subsystem.PixelCapture, label);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void OnRegionFrameReceived(ReadOnlySpan<byte> frame)
    {
        FrameCaptured.Invoke(frame);
        PtnshiftFinder.OnRegionCapture(frame);
    }

    private void OnFullScreenFrameReceived(ReadOnlySpan<byte> frame)
    {
        if (DisplayWidth == 0)
        {
            return;
        }

        PtnshiftFinder.OnFullScreenCapture(DisplayWidth, frame);
    }

    private SemaphoreSlim StoppedLock { get; } = new(1, 1);
    private long LastRestartTimestamp { get; set; }

    private async void OnCaptureStopped(CaptureStoppedEvent captureStoppedEvent)
    {
        try
        {
            Logger.LogInformation("Region capture stopped: {@Event}", captureStoppedEvent);

            if (captureStoppedEvent.Reason == StopReason.Voluntary)
            {
                Logger.LogInformation("Capture stopped voluntarily, ignoring");
                return;
            }

            var savedTimestamp = LastRestartTimestamp;
            Logger.LogInformation("Awaiting restart lock");
            await StoppedLock.WaitAsync();
            if (savedTimestamp > LastRestartTimestamp)
            {
                Logger.LogInformation("Restart already happened, ignoring");
                return;
            }

            LastRestartTimestamp = TimeProvider.GetTimestamp();
            StartStreamer();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to restart capture");
        }
        finally
        {
            if (StoppedLock.CurrentCount == 0)
            {
                StoppedLock.Release();
            }
        }
    }

    protected abstract Task UpdateStreamerConfigurationAsync(CaptureConfiguration previousConfiguration);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        StopCaptureInternal();
    }
}
