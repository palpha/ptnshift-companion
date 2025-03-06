using Core.Image;
using SkiaSharp;

namespace Tests.App;

public class ImageConverterMock : IImageConverter
{
    public byte[] Input { get; set; } = null!;
    public byte[] ExpectedOutput { get; set; } = null!;
    
    public void ConvertBgra24ToRgb16(ReadOnlySpan<byte> bgraBytes, Memory<byte> rgb16Bytes)
    {
        Input = bgraBytes.ToArray();
        ExpectedOutput.CopyTo(rgb16Bytes);
    }

    public SKBitmap ConvertBgra24BytesToBitmap(ReadOnlySpan<byte> receivedBytes, SKColorType colorType)
    {
        throw new NotImplementedException();
    }
}