namespace Core.Capturing;

public delegate void FrameCapturedHandler(ReadOnlySpan<byte> frameBytes);

public interface ICaptureEventSource
{
    event FrameCapturedHandler FrameCaptured;
    void InvokeFrameCaptured(ReadOnlySpan<byte> frameBytes);
}

public delegate void ExceptionHandler(Exception exception);

public interface IStreamer
{
    ICaptureEventSource EventSource { get; }
    Task<bool> CheckPermissionAsync();
    bool IsCapturing { get; }
    void Start(int displayId, int x, int y, int width, int height, int frameRate);
    void Stop();
}

public interface IFrameRateUpdater
{
    void SetFrameRate(int frameRate);
}