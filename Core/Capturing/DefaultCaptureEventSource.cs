using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public class DefaultCaptureEventSource(ILogger<DefaultCaptureEventSource> logger) : ICaptureEventSource
{
    private int LoggedFailures { get; set; }

    private ILogger<DefaultCaptureEventSource> Logger { get; } = logger;

    public event FrameCapturedHandler? RegionFrameCaptured;
    public event FrameCapturedHandler? FullScreenFrameCaptured;
    public event CaptureStoppedHandler? RegionCaptureStopped;
    public event CaptureStoppedHandler? FullScreenCaptureStopped;
    public event CaptureStateChangedHandler? CaptureStateChanged;

    public void InvokeFrameCaptured(FrameCaptureType type, ReadOnlySpan<byte> frameBytes)
    {
        if (type == FrameCaptureType.Region)
        {
            try
            {
                RegionFrameCaptured?.Invoke(frameBytes);
            }
            catch (Exception ex)
            {
                if (LoggedFailures++ % 300 == 0)
                {
                    Logger.LogError(
                        ex,
                        "Failed to invoke region frame captured event ({Count} failures)",
                        LoggedFailures);
                }
            }
        }
        else
        {
            FullScreenFrameCaptured?.Invoke(frameBytes);
        }
    }

    public void InvokeCaptureStopped(FrameCaptureType type, CaptureStoppedEvent eventArgs)
    {
        if (type == FrameCaptureType.Region)
        {
            RegionCaptureStopped?.Invoke(eventArgs);
        }
        else
        {
            FullScreenCaptureStopped?.Invoke(eventArgs);
        }
    }

    public void InvokeCaptureStateChanged(bool isCapturing)
    {
        CaptureStateChanged?.Invoke(isCapturing);
    }
}
