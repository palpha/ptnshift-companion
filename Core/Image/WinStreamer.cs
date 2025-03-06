using System.Runtime.InteropServices;

namespace Core.Image;

using System;

public class WinStreamer(ICaptureEventSource eventSource) : IStreamer, IDisposable
{
    private static WinScreenStreamLib.CaptureFrameCallback? CaptureCallback { get; set; }

    private int X { get; set; }
    private int Y { get; set; }
    private int Width { get; set; }
    private int Height { get; set; }

    public ICaptureEventSource EventSource { get; } = eventSource;

    public bool IsCapturing { get; private set; }

    public Task<bool> CheckPermissionAsync()
    {
        // On Windows, usually no special permission needed for direct duplication.
        return Task.FromResult(true);
    }

    public void Start(int displayId, int x, int y, int width, int height, int frameRate)
    {
        if (IsCapturing)
        {
            throw new InvalidOperationException("Capture already in progress.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;

        CaptureCallback = OnFrame;

        var result = WinScreenStreamLib.StartCapture(displayId, CaptureCallback, IntPtr.Zero);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to start capture: {result}");
        }

        IsCapturing = true;
    }

    public void Stop()
    {
        if (IsCapturing == false)
        {
            return;
        }

        Task.Run(() =>
        {
            WinScreenStreamLib.StopCapture();
            IsCapturing = false;
        });
    }

    ~WinStreamer() // Finalizer
    {
        Dispose();
    }

    public void Dispose()
    {
        Stop();
        WinScreenStreamLib.Cleanup();
        GC.SuppressFinalize(this);
    }

    private void OnFrame(IntPtr data, int width, int height, IntPtr userContext)
    {
        // If we’ve already stopped or got invalid data, bail out
        if (IsCapturing == false || width <= 0 || data == IntPtr.Zero)
        {
            return;
        }

        // Convert the data to a managed byte array
        var frameBytes = new byte[width * height * 4];
        Marshal.Copy(data, frameBytes, 0, frameBytes.Length);

        // Take a portion of the frame as specified by X, Y, Width, Height
        // assuming that the data is in BGRA8888 format
        // (4 bytes per pixel, with the first byte being blue, second green, third red, and fourth alpha)
        var croppedFrameBytes = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            var srcOffset = (Y + y) * width * 4 + X * 4;
            var destOffset = y * Width * 4;
            Buffer.BlockCopy(frameBytes, srcOffset, croppedFrameBytes, destOffset, Width * 4);
        }

        // Invoke the event
        EventSource.InvokeFrameCaptured(croppedFrameBytes);
    }
}