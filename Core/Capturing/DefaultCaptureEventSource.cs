namespace Core.Capturing;

public class DefaultCaptureEventSource : ICaptureEventSource
{
    public event FrameCapturedHandler? RegionFrameCaptured;
    public event FrameCapturedHandler? FullScreenFrameCaptured;

    public void InvokeFrameCaptured(FrameCaptureType type, ReadOnlySpan<byte> frameBytes)
    {
        if (type == FrameCaptureType.Region)
        {
            RegionFrameCaptured?.Invoke(frameBytes);
        }
        else
        {
            FullScreenFrameCaptured?.Invoke(frameBytes);
        }
    }
}