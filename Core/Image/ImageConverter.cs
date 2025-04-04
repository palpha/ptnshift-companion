using System.Runtime.InteropServices;
using SkiaSharp;

namespace Core.Image;

public interface IImageConverter
{
    void ConvertBgra32ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes);
    void ConvertRgb24ToRgb16(ReadOnlySpan<byte> rgbBytes, Memory<byte> rgb16Bytes);

    SKData ConvertToData(
        ReadOnlySpan<byte> frame,
        int? width = null, int? height = null,
        SKColorType? colorType = null);

    SKBitmap ConvertPixelBytesToBitmap(
        ReadOnlySpan<byte> receivedBytes,
        SKColorType colorType,
        int? width = null, int? height = null);

    void ScaleCpu(
        ReadOnlySpan<byte> inputFrame,
        int srcWidth,
        int srcHeight,
        Span<byte> outputFrame,
        int outWidth,
        int outHeight);
}

public class ImageConverter : IImageConverter
{
    private static byte[] XorPattern { get; } = [0xE7, 0xF3, 0xE7, 0xFF];

    public void ConvertBgra32ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes)
    {
        ushort ConvertPixelToRgb16(byte r, byte g, byte b)
        {
            // convert pixel to RGB565
            return (ushort) (((r & 0xF8) << 8) | // Red (5 bits) → bits 11-15
                ((g & 0xFC) << 3) | // Green (6 bits) → bits 5-10
                ((b & 0xF8) >> 3)); // Blue (5 bits) → bits 0-4
        }

        const int Width = 960;
        const int Height = 160;

        var outputSpan = rgb16Bytes.Span;
        var dstIndex = 0;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var index = (y * 960 + x) * 4;
                var (r, g, b) = (bgraBytes[index + 2], bgraBytes[index + 1], bgraBytes[index]);
                var rgb565 = ConvertPixelToRgb16(r, g, b);
                rgb565 = (ushort) (((rgb565 & 0x1F) << 11) | // Move Red to Blue position
                    (rgb565 & 0x07E0) | // Green unchanged
                    ((rgb565 & 0xF800) >> 11)); // Move Blue to Red position

                // Little-endian storage
                var lowByte = (byte) (rgb565 & 0xFF);
                var highByte = (byte) ((rgb565 >> 8) & 0xFF);

                // Apply XOR per byte
                outputSpan[dstIndex] = (byte) (lowByte ^ XorPattern[dstIndex % 4]);
                dstIndex++;
                outputSpan[dstIndex] = (byte) (highByte ^ XorPattern[dstIndex % 4]);
                dstIndex++;
            }

            // Add 128 bytes of row padding (not XORed)
            dstIndex += 128;
        }
    }

    public void ConvertRgb24ToRgb16(ReadOnlySpan<byte> rgbBytes, Memory<byte> rgb16Bytes)
    {
        static ushort ConvertPixelToRgb16(byte r, byte g, byte b)
        {
            // convert pixel to RGB565
            return (ushort) (((r & 0xF8) << 8) | // Red (5 bits) → bits 11-15
                ((g & 0xFC) << 3) | // Green (6 bits) → bits 5-10
                ((b & 0xF8) >> 3)); // Blue (5 bits) → bits 0-4
        }

        const int Width = 960;
        const int Height = 160;

        var outputSpan = rgb16Bytes.Span;
        var dstIndex = 0;

        try
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var index = (y * 960 + x) * 3;
                    var (r, g, b) = (rgbBytes[index], rgbBytes[index + 1], rgbBytes[index + 2]);
                    var rgb565 = ConvertPixelToRgb16(r, g, b);
                    rgb565 = (ushort) (((rgb565 & 0x1F) << 11) | // Move Red to Blue position
                        (rgb565 & 0x07E0) | // Green unchanged
                        ((rgb565 & 0xF800) >> 11)); // Move Blue to Red position

                    // Little-endian storage
                    var lowByte = (byte) (rgb565 & 0xFF);
                    var highByte = (byte) ((rgb565 >> 8) & 0xFF);

                    // Apply XOR per byte
                    outputSpan[dstIndex] = (byte) (lowByte ^ XorPattern[dstIndex % 4]);
                    dstIndex++;
                    outputSpan[dstIndex] = (byte) (highByte ^ XorPattern[dstIndex % 4]);
                    dstIndex++;
                }

                // Add 128 bytes of row padding (not XORed)
                dstIndex += 128;
            }
        }
        catch (Exception ex)
        {
            //
        }
    }

    public SKData ConvertToData(ReadOnlySpan<byte> frame, int? width = null, int? height = null,
        SKColorType? colorType = null)
    {
        var bitmap = ConvertPixelBytesToBitmap(frame, colorType ?? SKColorType.Bgra8888, width, height);
        using var skImage = SKImage.FromBitmap(bitmap);
        return skImage.Encode(SKEncodedImageFormat.Png, 100);
    }

    public SKBitmap ConvertPixelBytesToBitmap(
        ReadOnlySpan<byte> receivedBytes,
        SKColorType colorType,
        int? width = null, int? height = null)
    {
        var imageInfo = new SKImageInfo(width ?? 960, height ?? 160, colorType);

        // Get a reference to the first element in the span (zero-copy)
        ref var firstByte = ref MemoryMarshal.GetReference(receivedBytes);

        unsafe
        {
            fixed (byte* ptr = &firstByte)
            {
                var bitmap = new SKBitmap();
                if (bitmap.InstallPixels(imageInfo, (IntPtr) ptr) == false)
                {
                    throw new("Failed to install pixels into SKBitmap.");
                }

                return bitmap;
            }
        }
    }

    public void ScaleCpu(
        ReadOnlySpan<byte> inputFrame,
        int srcWidth,
        int srcHeight,
        Span<byte> outputFrame,
        int outWidth,
        int outHeight)
    {
        if (outputFrame.Length < outWidth * outHeight * 3)
        {
            throw new ArgumentException("outputFrame is too small for the requested output size.");
        }

        if (srcWidth <= 0 || srcHeight <= 0
            || outWidth <= 0 || outHeight <= 0)
        {
            throw new InvalidOperationException("Invalid image dimensions.");
        }

        // Bilinear scale factors
        var scaleX = (float) srcWidth / outWidth;
        var scaleY = (float) srcHeight / outHeight;

        for (var outY = 0; outY < outHeight; outY++)
        {
            // Map the Y of the output back into the input
            var srcFloatY = (outY + 0.5f) * scaleY - 0.5f;
            var srcY0 = (int) Math.Floor(srcFloatY);
            var fy = srcFloatY - srcY0;

            // Clamp so we don't go out of bounds
            if (srcY0 < 0)
            {
                srcY0 = 0;
                fy = 0;
            }

            if (srcY0 >= srcHeight - 1)
            {
                srcY0 = srcHeight - 2;
                fy = 1.0f;
            }

            var srcY1 = srcY0 + 1;

            for (var outX = 0; outX < outWidth; outX++)
            {
                var srcFloatX = (outX + 0.5f) * scaleX - 0.5f;
                var srcX0 = (int) Math.Floor(srcFloatX);
                var fx = srcFloatX - srcX0;

                if (srcX0 < 0)
                {
                    srcX0 = 0;
                    fx = 0;
                }

                if (srcX0 >= srcWidth - 1)
                {
                    srcX0 = srcWidth - 2;
                    fx = 1.0f;
                }

                var srcX1 = srcX0 + 1;

                // Indices in the source array (3 BPP)
                var i00 = (srcY0 * srcWidth + srcX0) * 3;
                var i01 = (srcY0 * srcWidth + srcX1) * 3;
                var i10 = (srcY1 * srcWidth + srcX0) * 3;
                var i11 = (srcY1 * srcWidth + srcX1) * 3;

                // Destination index
                var outIdx = (outY * outWidth + outX) * 3;

                // Interpolate each color channel
                for (var c = 0; c < 3; c++)
                {
                    float c00 = inputFrame[i00 + c];
                    float c01 = inputFrame[i01 + c];
                    float c10 = inputFrame[i10 + c];
                    float c11 = inputFrame[i11 + c];

                    var c0 = c00 + fx * (c01 - c00);
                    var c1 = c10 + fx * (c11 - c10);
                    var cOut = c0 + fy * (c1 - c0);

                    outputFrame[outIdx + c] = (byte) Math.Round(cOut);
                }
            }
        }
    }
}
