using Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public abstract class CaptureServiceBase(
    IStreamer streamer,
    IDisplayService displayService,
    IPtnshiftFinder ptnshiftFinder,
    IDiagnosticOutputRenderer diagnosticOutputRenderer,
    ILogger<CaptureServiceBase> logger)
    : ICaptureService
{
    protected IStreamer Streamer { get; } = streamer;

    private IDisplayService DisplayService { get; } = displayService;
    private IPtnshiftFinder PtnshiftFinder { get; } = ptnshiftFinder;
    private IDiagnosticOutputRenderer DiagnosticOutputRenderer { get; } = diagnosticOutputRenderer;

    private int DisplayWidth { get; set; }

    protected ILogger<CaptureServiceBase> Logger { get; } = logger;
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
            UpdateStreamerConfiguration(previousConfiguration);
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
        StartStreamer();

        Logger.LogInformation("Started capture");
    }

    public void StopCapture()
    {
        Logger.LogInformation("Stopping capture");

        if (IsCapturing == false)
        {
            Logger.LogInformation("Was not capturing");

            return;
        }

        Streamer.EventSource.RegionFrameCaptured -= OnRegionFrameReceived;
        Streamer.EventSource.FullScreenFrameCaptured -= OnFullScreenFrameReceived;
        StopStreamer();

        Logger.LogInformation("Stopped capture");
    }

    protected void StopStreamer()
    {
        Streamer.Stop();
        DiagnosticOutputRenderer.SetText(Subsystem.PixelCapture, "");
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

    protected abstract void UpdateStreamerConfiguration(CaptureConfiguration previousConfiguration);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        StopCapture();
    }
}
