using System.Runtime.InteropServices;

namespace Core.Capturing;

using System;

public class WindowsStreamer(
    ICaptureEventSource eventSource,
    TimeProvider timeProvider)
    : IStreamer, IFrameRateUpdater, IDisposable
{
    private static WinScreenStreamLib.CaptureFrameCallback? CaptureCallback { get; set; }

    private int X { get; set; }
    private int Y { get; set; }
    private int Width { get; set; }
    private int Height { get; set; }
    private long LastFullScreenTimestamp { get; set; }

    private TimeProvider TimeProvider { get; } = timeProvider;

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

        var result = WinScreenStreamLib.StartCapture(displayId, frameRate, CaptureCallback, nint.Zero);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to start capture: {result}");
        }

        IsCapturing = true;
    }

    public void SetFrameRate(int frameRate)
    {
        WinScreenStreamLib.SetFrameRate(frameRate);
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

    ~WindowsStreamer() // Finalizer
    {
        Dispose();
    }

    public void Dispose()
    {
        Stop();
        WinScreenStreamLib.Cleanup();
        GC.SuppressFinalize(this);
    }

    private byte[]? FullScreenBuffer { get; set; }
    private byte[]? RegionFrameBuffer { get; set; }

    private void OnFrame(nint data, int width, int height, nint userContext)
    {
        // If we've already stopped or got invalid data, bail out
        if (IsCapturing == false || width <= 0 || data == nint.Zero)
        {
            return;
        }

        // Convert the data to a managed byte array
        var frameSize = width * height * 4;
        FullScreenBuffer = FullScreenBuffer?.Length >= frameSize
            ? FullScreenBuffer
            : new byte[frameSize];
        Marshal.Copy(data, FullScreenBuffer, 0, frameSize);

        if (LastFullScreenTimestamp == 0
            || TimeProvider.GetElapsedTime(LastFullScreenTimestamp) > TimeSpan.FromSeconds(1))
        {
            // Call the full screen frame captured event
            EventSource.InvokeFrameCaptured(FrameCaptureType.FullScreen, FullScreenBuffer);
            LastFullScreenTimestamp = TimeProvider.GetTimestamp();
        }

        // Take a portion of the frame as specified by X, Y, Width, Height
        // assuming that the data is in BGRA8888 format
        // (4 bytes per pixel, with the first byte being blue, second green, third red, and fourth alpha)
        var regionSize = Width * Height * 4;
        RegionFrameBuffer = RegionFrameBuffer?.Length >= regionSize
            ? RegionFrameBuffer
            : new byte[regionSize];

        for (var y = 0; y < Height; y++)
        {
            var srcOffset = (Y + y) * width * 4 + X * 4;
            var destOffset = y * Width * 4;
            Buffer.BlockCopy(FullScreenBuffer, srcOffset, RegionFrameBuffer, destOffset, Width * 4);
        }

        // Invoke the event
        EventSource.InvokeFrameCaptured(FrameCaptureType.Region, RegionFrameBuffer);
    }
}