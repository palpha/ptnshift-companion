using System.Buffers;
using System.Runtime.InteropServices;

namespace Core.Capturing;

using System;

public class WindowsStreamer(
    ICaptureEventSource eventSource,
    TimeProvider timeProvider)
    : IStreamer, IFrameRateUpdater, IRegionUpdater, IDisposable
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

    public void SetRegion(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
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

    private void SendFullScreen(nint data, int width, int height)
    {
        var frameSize = width * height * 3;
        var buffer = ArrayPool<byte>.Shared.Rent(frameSize);
        try
        {
            Marshal.Copy(data, buffer, 0, frameSize);
            EventSource.InvokeFrameCaptured(FrameCaptureType.FullScreen, buffer);
            LastFullScreenTimestamp = TimeProvider.GetTimestamp();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void SendRegion(nint data, int width, int height)
    {
        var rowSize = Width * 3; // 3 bytes per pixel
        var bufferSize = rowSize * Height;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            for (var row = 0; row < Height; row++)
            {
                // Calculate where to read from in the native data
                var srcOffset = (Y + row) * width * 3 + X * 3;

                // Calculate into which offset in the RegionFrameBuffer
                var dstOffset = row * rowSize;

                Marshal.Copy(IntPtr.Add(data, srcOffset), buffer, dstOffset, rowSize);
            }

            EventSource.InvokeFrameCaptured(FrameCaptureType.Region, buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void OnFrame(nint data, int width, int height, nint userContext)
    {
        // If we've already stopped or got invalid data, bail out
        if (IsCapturing == false || width <= 0 || data == nint.Zero)
        {
            return;
        }

        if (LastFullScreenTimestamp == 0
            || TimeProvider.GetElapsedTime(LastFullScreenTimestamp) > TimeSpan.FromSeconds(1))
        {
            SendFullScreen(data, width, height);
        }

        SendRegion(data, width, height);
    }
}