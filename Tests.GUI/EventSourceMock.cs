using Core.Capturing;

namespace Tests.GUI;

public class EventSourceMock : ICaptureEventSource
{
    public int FrameCapturedHandlerAddCount { get; private set; }
    public int FrameCapturedHandlerRemoveCount { get; private set; }

    public List<ReadOnlyMemory<byte>> Frames { get; } = [];

    private event FrameCapturedHandler? InternalFrameCaptured;

    public event FrameCapturedHandler? RegionFrameCaptured
    {
        add
        {
            FrameCapturedHandlerAddCount++;
            InternalFrameCaptured += value;
        }
        remove
        {
            FrameCapturedHandlerRemoveCount++;
            InternalFrameCaptured -= value;
        }
    }

    public event FrameCapturedHandler? FullScreenFrameCaptured;
    public event CaptureStoppedHandler? RegionCaptureStopped;
    public event CaptureStoppedHandler? FullScreenCaptureStopped;

    public void InvokeFrameCaptured(FrameCaptureType type, ReadOnlySpan<byte> frameBytes)
    {
        if (type != FrameCaptureType.Region)
        {
            return;
        }

        Frames.Add(frameBytes.ToArray());
        InternalFrameCaptured?.Invoke(frameBytes);
    }

    public void InvokeCaptureStopped(FrameCaptureType type, CaptureStoppedEvent eventArgs)
    {
        throw new NotImplementedException();
    }
}
