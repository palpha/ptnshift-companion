using System.Buffers;
using Avalonia.Media.Imaging;
using Core.Capturing;
using Microsoft.Extensions.Logging;

namespace Core.Image;

public interface IPreviewRenderer
{
    event Action<WriteableBitmap> PreviewRendered;
    bool IsPreviewEnabled { get; set; }
}

public class PreviewRenderer : IPreviewRenderer
{
    private ILogger<PreviewRenderer> Logger { get; }

    private Lock BitmapLock { get; } = new();

    private WriteableBitmap? ImageSource { get; set; }
    private WriteableBitmap? PreviewBitmap { get; set; }

    public event Action<WriteableBitmap> PreviewRendered = delegate { };

    public bool IsPreviewEnabled { get; set; }

    public PreviewRenderer(
        ICaptureService captureService,
        ILogger<PreviewRenderer> logger)
    {
        Logger = logger;

        captureService.FrameCaptured += OnFrameReceived;
    }

    private void OnFrameReceived(ReadOnlySpan<byte> frame)
    {
        if (IsPreviewEnabled == false)
        {
            // Ignore frames if preview is not requested
            return;
        }

        const int Width = 960;
        const int TotalRows = 161; // total rows in the incoming frame
        const int PreviewRows = 160; // rows to display (skipping the first row)
        const int SrcStride = Width * 3;
        const int DstStride = Width * 4;

        // Convert the RGB24 frame to BGRA32, skipping the first row
        var rgbaFrame = ArrayPool<byte>.Shared.Rent(960 * 160 * 4);
        try
        {
            for (var row = 1; row < TotalRows; row++)
            {
                var srcRowStart = row * SrcStride;
                var dstRowStart = (row - 1) * DstStride;
                for (var col = 0; col < Width; col++)
                {
                    var srcIndex = srcRowStart + col * 3;
                    var dstIndex = dstRowStart + col * 4;
                    var r = frame[srcIndex];
                    var g = frame[srcIndex + 1];
                    var b = frame[srcIndex + 2];
                    rgbaFrame[dstIndex] = b; // Blue
                    rgbaFrame[dstIndex + 1] = g; // Green
                    rgbaFrame[dstIndex + 2] = r; // Red
                    rgbaFrame[dstIndex + 3] = 255; // Alpha (opaque)
                }
            }

            lock (BitmapLock)
            {
                PreviewBitmap ??= new(
                    new(960, 160),
                    new(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Premul);

                using var lockedFramebuffer = PreviewBitmap.Lock();

                unsafe
                {
                    const int PreviewLength = Width * PreviewRows * 4;
                    var dstSpan = new Span<byte>(lockedFramebuffer.Address.ToPointer(), PreviewLength);
                    rgbaFrame.AsSpan(0, PreviewLength).CopyTo(dstSpan);
                }

                // Swap buffers
                (ImageSource, PreviewBitmap) = (PreviewBitmap, ImageSource);

                PreviewRendered.Invoke(ImageSource);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to render preview");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rgbaFrame);
        }
    }
}
