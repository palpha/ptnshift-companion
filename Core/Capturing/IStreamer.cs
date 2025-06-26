namespace Core.Capturing;

public delegate void FrameCapturedHandler(ReadOnlySpan<byte> frameBytes);

public delegate void CaptureStoppedHandler(CaptureStoppedEvent eventArgs);

public delegate void CaptureStateChangedHandler(bool isCapturing);

public enum FrameCaptureType
{
    Region,
    FullScreen
}

public enum StopReason
{
    Unknown,
    Voluntary
}

public record CaptureStoppedEvent(int ErrorCode, string ErrorMessage, StopReason Reason);

public interface ICaptureEventSource
{
    event FrameCapturedHandler RegionFrameCaptured;
    event FrameCapturedHandler FullScreenFrameCaptured;
    event CaptureStoppedHandler RegionCaptureStopped;
    event CaptureStoppedHandler FullScreenCaptureStopped;
    event CaptureStateChangedHandler? CaptureStateChanged;
    void InvokeFrameCaptured(FrameCaptureType type, ReadOnlySpan<byte> frameBytes);
    void InvokeCaptureStopped(FrameCaptureType type, CaptureStoppedEvent eventArgs);
    void InvokeCaptureStateChanged(bool isCapturing);
}

public interface IStreamer
{
    ICaptureEventSource EventSource { get; }
    Task<bool> CheckPermissionAsync();
    bool IsCapturing { get; }
    void Start(int displayId, int x, int y, int width, int height, int frameRate);
    Task StopAsync();
}

public interface IRegionUpdater
{
    void SetRegion(int x, int y, int width, int height);
}

public interface IFrameRateUpdater
{
    void SetFrameRate(int frameRate);
}
