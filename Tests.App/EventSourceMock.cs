using Core.Capturing;

namespace Tests.App;

public class EventSourceMock : ICaptureEventSource
{
    public int FrameCapturedHandlerAddCount { get; private set; }
    public int FrameCapturedHandlerRemoveCount { get; private set; }

    public List<ReadOnlyMemory<byte>> Frames { get; } = [];

    private event FrameCapturedHandler? InternalFrameCaptured;
    
    public event FrameCapturedHandler? FrameCaptured
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

    public void InvokeFrameCaptured(ReadOnlySpan<byte> frameBytes)
    {
        Frames.Add(frameBytes.ToArray());
        InternalFrameCaptured?.Invoke(frameBytes);
    }
}