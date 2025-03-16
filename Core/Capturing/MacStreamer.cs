using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Tests.GUI")]

namespace Core.Capturing;

public class MacStreamer(
    ICaptureEventSource eventSource,
    IDisplayService displayService) : IStreamer, IDisposable
{
    private IDisplayService DisplayService { get; } = displayService;
    private Lock ArrayPoolLock { get; } = new();
    private Lock FrameLock { get; } = new();
    private byte[] RegionFrameBuffer { get; } = new byte[960 * 161 * 4];
    private byte[]? FullScreenFrameBuffer { get; set; }
    private static LibScreenStream.CaptureCallback? RegionCaptureCallback { get; set; }
    private static LibScreenStream.CaptureCallback? FullScreenCaptureCallback { get; set; }

    public ICaptureEventSource EventSource { get; } = eventSource;

    public bool IsCapturing { get; private set; }

    public async Task<bool> CheckPermissionAsync()
    {
        LibScreenStream.CheckCapturePermission();
        await Task.Delay(100);
        return LibScreenStream.IsCapturePermissionGranted();
    }

    public void Start(int displayId, int x, int y, int width, int height, int frameRate)
    {
        if (IsCapturing)
        {
            throw new InvalidOperationException("Capture already in progress.");
        }

        var display = DisplayService.GetDisplay(displayId);

        if (display == null)
        {
            throw new InvalidOperationException("Display could not be found.");
        }

        RegionCaptureCallback = OnFrame(RegionFrameBuffer, FrameCaptureType.Region);

        var fullScreenCaptureBufferSize = display.Width * display.Height * 4;
        lock (ArrayPoolLock)
        {
        }

        FullScreenFrameBuffer = ArrayPool<byte>.Shared.Rent(fullScreenCaptureBufferSize);
        FullScreenCaptureCallback = OnFrame(FullScreenFrameBuffer, FrameCaptureType.FullScreen);

        var result = LibScreenStream.StartCapture(
            displayId,
            x, y,
            width, height,
            frameRate, fullScreenFrameRate: 1,
            RegionCaptureCallback,
            FullScreenCaptureCallback);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to start capture: {result}");
        }

        IsCapturing = true;
    }

    private LibScreenStream.CaptureCallback OnFrame(byte[] frameBuffer, FrameCaptureType type)
    {
        return (data, length) =>
        {
            if (IsCapturing == false || length <= 0 || data == nint.Zero)
            {
                return;
            }

            if (length > frameBuffer.Length)
            {
                return;
            }

            lock (FrameLock)
            {
                try
                {
                    Marshal.Copy(data, frameBuffer, 0, length);
                    EventSource.InvokeFrameCaptured(type, frameBuffer.AsSpan(0, length));
                }
                catch (Exception)
                {
                    //
                }
            }
        };
    }

    public void Stop()
    {
        if (IsCapturing == false)
        {
            return;
        }

        lock (ArrayPoolLock)
        {
            if (FullScreenFrameBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(FullScreenFrameBuffer);
            }
        }

        var result = LibScreenStream.StopCapture();
        if (result != 0)
        {
            //
        }

        RegionCaptureCallback = null;
        FullScreenCaptureCallback = null;

        IsCapturing = false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Stop();
    }
}