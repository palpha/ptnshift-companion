namespace Core.Capturing;

public delegate void FrameCapturedHandler(ReadOnlySpan<byte> frameBytes);

public enum FrameCaptureType
{
    Region,
    FullScreen
}

public interface ICaptureEventSource
{
    event FrameCapturedHandler RegionFrameCaptured;
    event FrameCapturedHandler FullScreenFrameCaptured;
    void InvokeFrameCaptured(FrameCaptureType type, ReadOnlySpan<byte> frameBytes);
}

public interface IStreamer
{
    ICaptureEventSource EventSource { get; }
    Task<bool> CheckPermissionAsync();
    bool IsCapturing { get; }
    void Start(int displayId, int x, int y, int width, int height, int frameRate);
    void Stop();
}

public interface IRegionUpdater
{
    void SetRegion(int x, int y, int width, int height);
}

public interface IFrameRateUpdater
{
    void SetFrameRate(int frameRate);
}
