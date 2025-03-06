using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Tests.App")]

namespace Core.Image;

public class MacStreamer(ICaptureEventSource eventSource) : IStreamer, IDisposable
{
    private byte[]? FrameBuffer { get; set; }

    public bool IsCapturing { get; private set; }

    public ICaptureEventSource EventSource { get; } = eventSource;

    public async Task<bool> CheckPermissionAsync()
    {
        LibScreenStream.CheckCapturePermission();
        await Task.Delay(100);
        return LibScreenStream.IsCapturePermissionGranted();
    }

    private Lock FrameLock { get; } = new();

    private static LibScreenStream.CaptureCallback? CaptureCallback { get; set; }

    public void Start(int displayId, int x, int y, int width, int height, int frameRate)
    {
        if (IsCapturing)
            throw new InvalidOperationException("Capture already in progress.");

        CaptureCallback = OnFrame;

        // Prepare FrameBuffer based on expected size
        var bufferSize = width * height * 4;
        if (FrameBuffer is null || FrameBuffer.Length != bufferSize)
        {
            FrameBuffer = new byte[bufferSize];
        }

        var result = LibScreenStream.StartCapture(displayId, x, y, width, height, frameRate, CaptureCallback);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to start capture: {result}");
        }

        IsCapturing = true;
    }

    private void OnFrame(IntPtr data, int length)
    {
        // If we’ve already stopped or got invalid data, bail out
        if (!IsCapturing || length <= 0 || data == IntPtr.Zero || FrameBuffer is null)
            return;

        // Clamp/resize if native side gave a bigger-than-expected length
        if (length > FrameBuffer.Length)
        {
            // Option 1: Skip the frame
            return;

            // Option 2: Resize and continue
            //Array.Resize(ref FrameBuffer, length);
        }

        lock (FrameLock)
        {
            try
            {
                // Copy from unmanaged memory into FrameBuffer
                Marshal.Copy(data, FrameBuffer, 0, length);

                // Hand off the frame to your event source
                EventSource.InvokeFrameCaptured(FrameBuffer.AsSpan(0, length));
            }
            catch (Exception)
            {
                // Log or handle any unexpected Marshal.Copy issues
            }
        }
    }

    public void Stop()
    {
        if (!IsCapturing)
            return;

        var result = LibScreenStream.StopCapture();
        if (result != 0)
        {
            // Decide how to handle errors
        }

        IsCapturing = false;
    }

    public void Dispose()
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