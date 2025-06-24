using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Tests.GUI")]

namespace Core.Capturing;

public class MacStreamer(
    ICaptureEventSource eventSource,
    IDisplayService displayService,
    ILogger<MacStreamer> logger) : IStreamer, IDisposable
{
    private IDisplayService DisplayService { get; } = displayService;
    private ILogger<MacStreamer> Logger { get; } = logger;

    private Lock FrameLock { get; } = new();

    private int LoggedFrameFailures { get; set; }

    public ICaptureEventSource EventSource { get; } = eventSource;

    public bool IsCapturing { get; private set; }

    public async Task<bool> CheckPermissionAsync()
    {
        LibScreenStream.CheckCapturePermission();
        await Task.Delay(100);
        return LibScreenStream.IsCapturePermissionGranted();
    }

    private static LibScreenStream.CaptureCallback? RegionCaptureCallback { get; set; }
    private static LibScreenStream.CaptureCallback? FullScreenCaptureCallback { get; set; }

    public void Start(int displayId, int x, int y, int width, int height, int frameRate)
    {
        Logger.LogInformation(
            "Starting capture at {X}, {Y}, {Width}x{Height}, {FrameRate} FPS",
            x, y, width, height, frameRate);

        if (IsCapturing)
        {
            Logger.LogWarning("Capture is already running");
            throw new InvalidOperationException("Capture already in progress.");
        }

        var display = DisplayService.GetDisplay(displayId);
        if (display == null)
        {
            Logger.LogWarning("Display {DisplayId} not found", displayId);
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
            Logger.LogError("Failed to start capture, HRESULT {HResult}", result);
            throw new InvalidOperationException($"Failed to start capture: {result}");
        }

        IsCapturing = true;

        Logger.LogInformation("Started capture");
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
                catch (Exception ex)
                {
                    if (LoggedFrameFailures++ % 300 == 0)
                    {
                        Logger.LogError(
                            ex,
                            "Failed to capture frame ({Count} failures)",
                            LoggedFrameFailures);
                    }
                }
            }
        };
    }

    public void Stop()
    {
        Logger.LogInformation("Stopping capture");

        if (Buffers.Count > 0)
        {
            Logger.LogInformation("Returning buffers");

            foreach (var buffer in Buffers.Values)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            Buffers.Clear();
        }

        if (IsCapturing == false)
        {
            Logger.LogInformation("Was not capturing");
            return;
        }

        var result = LibScreenStream.StopCapture();
        if (result != 0)
        {
            Logger.LogWarning("Non-zero result when stopping capture: {HResult}", result);
        }

        IsCapturing = false;

        Logger.LogInformation("Stopped capture");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Stop();
    }
}
