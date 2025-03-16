using Core.Image;
using SkiaSharp;

namespace Tests.GUI;

public class ImageConverterMock : IImageConverter
{
    public byte[] Input { get; set; } = null!;
    public byte[] ExpectedOutput { get; set; } = null!;
    
    public void ConvertBgra24ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes)
    {
        Input = bgraBytes.ToArray();
        ExpectedOutput.CopyTo(rgb16Bytes);
    }

    public SKData ConvertToData(ReadOnlySpan<byte> frame, int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }

    public SKBitmap ConvertBgra24BytesToBitmap(
        ReadOnlySpan<byte> receivedBytes,
        SKColorType colorType,
        int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }
}