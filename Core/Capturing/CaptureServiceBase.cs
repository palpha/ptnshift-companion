using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public abstract class CaptureServiceBase(
    IStreamer streamer,
    IDisplayService displayService,
    ILogger<CaptureServiceBase> logger)
    : ICaptureService
{
    protected IStreamer Streamer { get; } = streamer;

    private IDisplayService DisplayService { get; } = displayService;

    protected ILogger<CaptureServiceBase> Logger { get; } = logger;
    protected CaptureConfiguration? CurrentConfiguration { get; set; }

    public event Action<ReadOnlySpan<byte>>? FrameCaptured;

    public bool IsCapturing => Streamer.IsCapturing;

    public abstract Task<bool> CheckCapturePermissionAsync();

    public virtual void SetConfiguration(CaptureConfiguration configuration)
    {
        var previousConfiguration = CurrentConfiguration;
        CurrentConfiguration = configuration;

        if (previousConfiguration != null)
        {
            UpdateStreamerConfiguration(previousConfiguration);
        }
    }

    public virtual void StartCapture()
    {
        if (CurrentConfiguration == null)
        {
            throw new InvalidOperationException("Configuration not set.");
        }

        if (IsCapturing || CurrentConfiguration.IsValid(DisplayService.AvailableDisplays) == false)
        {
            return;
        }

        Streamer.EventSource.FrameCaptured += OnFrameReceived;
        StartStreamer();
    }

    public void StopCapture()
    {
        if (IsCapturing == false)
        {
            return;
        }

        Streamer.EventSource.FrameCaptured -= OnFrameReceived;

        Streamer.Stop();
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
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to start capture");
            throw;
        }
    }

    private void OnFrameReceived(ReadOnlySpan<byte> frame) =>
        FrameCaptured?.Invoke(frame);

    protected abstract void UpdateStreamerConfiguration(CaptureConfiguration previousConfiguration);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        StopCapture();
    }
}