using Core.Image;
using SkiaSharp;

namespace Tests.GUI;

public class ImageConverterMock : IImageConverter
{
    public byte[] Input { get; set; } = null!;
    public byte[] ExpectedOutput { get; set; } = null!;
    
    public void ConvertBgra32ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes)
    {
        Input = bgraBytes.ToArray();
        ExpectedOutput.CopyTo(rgb16Bytes);
    }
    
    public void ConvertRgb24ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes)
    {
        ConvertBgra32ToRgb16(bgraBytes, rgb16Bytes);
    }

    public SKData ConvertToData(ReadOnlySpan<byte> frame, int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }

    public SKBitmap ConvertPixelBytesToBitmap(
        ReadOnlySpan<byte> receivedBytes,
        SKColorType colorType,
        int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }
}