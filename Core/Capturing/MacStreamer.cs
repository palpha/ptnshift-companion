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

    public ICaptureEventSource EventSource { get; } = eventSource;

    public bool IsCapturing { get; private set; }

    public async Task<bool> CheckPermissionAsync()
    {
        LibScreenStream.CheckCapturePermission();
        await Task.Delay(100);
        return LibScreenStream.IsCapturePermissionGranted();
    }

    private Lock FrameLock { get; } = new();

    private static LibScreenStream.CaptureCallback? RegionCaptureCallback { get; set; }
    private static LibScreenStream.CaptureCallback? FullScreenCaptureCallback { get; set; }

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

        var regionBufferSize = width * height * 3;
        RegionCaptureCallback = OnFrame(regionBufferSize, FrameCaptureType.Region);

        var fullScreenBufferSize = display.Width * display.Height * 3;
        FullScreenCaptureCallback = OnFrame(fullScreenBufferSize, FrameCaptureType.FullScreen);

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

    private Dictionary<FrameCaptureType, byte[]> Buffers { get; } = new();

    private LibScreenStream.CaptureCallback OnFrame(int bufferSize, FrameCaptureType type)
    {
        if (Buffers.ContainsKey(type))
        {
            throw new InvalidOperationException("Capture already in progress.");
        }

        var frameBuffer = Buffers[type] = ArrayPool<byte>.Shared.Rent(bufferSize);

        return (data, length) =>
        {
            if (IsCapturing == false || length <= 0 || data == nint.Zero)
            {
                return;
            }

            if (length > bufferSize)
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
        if (Buffers.Count > 0)
        {
            foreach (var buffer in Buffers.Values)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            Buffers.Clear();
        }

        if (IsCapturing == false)
        {
            return;
        }

        var result = LibScreenStream.StopCapture();
        if (result != 0)
        {
            //
        }

        IsCapturing = false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Stop();
    }
}