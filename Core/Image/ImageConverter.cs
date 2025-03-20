using System.Runtime.InteropServices;
using SkiaSharp;

namespace Core.Image;

public interface IImageConverter
{
    void ConvertBgra32ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes);
    void ConvertRgb24ToRgb16(ReadOnlySpan<byte> rgbBytes, Memory<byte> rgb16Bytes);

    SKData ConvertToData(ReadOnlySpan<byte> frame, int? width = null, int? height = null);

    SKBitmap ConvertPixelBytesToBitmap(
        ReadOnlySpan<byte> receivedBytes,
        SKColorType colorType,
        int? width = null, int? height = null);
}

public class ImageConverter : IImageConverter
{
    private static byte[] XorPattern { get; } = [0xE7, 0xF3, 0xE7, 0xFF];

    public void ConvertBgra32ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes)
    {
        ushort ConvertPixelToRgb16(byte r, byte g, byte b)
        {
            // convert pixel to RGB565
            return (ushort)(((r & 0xF8) << 8) | // Red (5 bits) → bits 11-15
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
                rgb565 = (ushort)(((rgb565 & 0x1F) << 11) | // Move Red to Blue position
                    (rgb565 & 0x07E0) | // Green unchanged
                    ((rgb565 & 0xF800) >> 11)); // Move Blue to Red position

                // Little-endian storage
                var lowByte = (byte)(rgb565 & 0xFF);
                var highByte = (byte)((rgb565 >> 8) & 0xFF);

                // Apply XOR per byte
                outputSpan[dstIndex] = (byte)(lowByte ^ XorPattern[dstIndex % 4]);
                dstIndex++;
                outputSpan[dstIndex] = (byte)(highByte ^ XorPattern[dstIndex % 4]);
                dstIndex++;
            }

            // Add 128 bytes of row padding (not XORed)
            dstIndex += 128;
        }
    }
    
    public void ConvertRgb24ToRgb16(ReadOnlySpan<byte> rgbBytes, Memory<byte> rgb16Bytes)
    {
        ushort ConvertPixelToRgb16(byte r, byte g, byte b)
        {
            // convert pixel to RGB565
            return (ushort)(((r & 0xF8) << 8) | // Red (5 bits) → bits 11-15
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
                    rgb565 = (ushort)(((rgb565 & 0x1F) << 11) | // Move Red to Blue position
                        (rgb565 & 0x07E0) | // Green unchanged
                        ((rgb565 & 0xF800) >> 11)); // Move Blue to Red position

                    // Little-endian storage
                    var lowByte = (byte)(rgb565 & 0xFF);
                    var highByte = (byte)((rgb565 >> 8) & 0xFF);

                    // Apply XOR per byte
                    outputSpan[dstIndex] = (byte)(lowByte ^ XorPattern[dstIndex % 4]);
                    dstIndex++;
                    outputSpan[dstIndex] = (byte)(highByte ^ XorPattern[dstIndex % 4]);
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
    
    public SKData ConvertToData(ReadOnlySpan<byte> frame, int? width = null, int? height = null)
    {
        var bitmap = ConvertPixelBytesToBitmap(frame, SKColorType.Bgra8888, width, height);
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
                if (bitmap.InstallPixels(imageInfo, (IntPtr)ptr) == false)
                {
                    throw new("Failed to install pixels into SKBitmap.");
                }

                return bitmap;
            }
        }
    }
}
