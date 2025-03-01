using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Tests.App")]

namespace Core.Image;

public class Streamer(ICaptureEventSource eventSource) : IStreamer, IDisposable
{
    private byte[]? FrameBuffer { get; set; }

    public bool IsCapturing { get; private set; }

    public ICaptureEventSource EventSource { get; } = eventSource;

    public virtual async Task<bool> CheckPermissionAsync()
    {
        LibScreenStream.CheckCapturePermission();
        await Task.Delay(100);
        return LibScreenStream.IsCapturePermissionGranted();
    }

    public virtual void Start(int displayId, int x, int y, int width, int height, int frameRate)
    {
        if (IsCapturing)
        {
            throw new InvalidOperationException("Capture already in progress.");
        }

        var bufferSize = width * height * 4;
        if (FrameBuffer is null || FrameBuffer.Length != bufferSize)
        {
            FrameBuffer = new byte[bufferSize];
        }

        var result = LibScreenStream.StartCapture(displayId, x, y, width, height, frameRate, OnFrame);
        if (result != 0)
        {
            throw new($"Failed to start capture: {result}");
        }

        IsCapturing = true;
    }

    public virtual void Stop()
    {
        var result = LibScreenStream.StopCapture();
        if (result != 0)
        {
            //
        }

        IsCapturing = false;
    }

    private void OnFrame(IntPtr data, int length)
    {
        if (length <= 0 || data == IntPtr.Zero || FrameBuffer is null)
        {
            return;
        }

        Marshal.Copy(data, FrameBuffer, 0, length);
        EventSource.InvokeFrameCaptured(FrameBuffer.AsSpan(0, length));
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        Stop();
    }
}

public class DefaultCaptureEventSource : ICaptureEventSource
{
    public event FrameCapturedHandler? FrameCaptured;

    public void InvokeFrameCaptured(ReadOnlySpan<byte> frameBytes)
    {
        FrameCaptured?.Invoke(frameBytes);
    }
}