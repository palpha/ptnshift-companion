using System.Diagnostics.CodeAnalysis;
using Core.Image;
using Shouldly;
using SkiaSharp;

namespace Tests.GUI;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class ImageConverterTests
{
    private ImageConverter Sut { get; } = new();

    private static byte[] GetBytesFromFile(string fileName) => File.ReadAllBytes(fileName);

    [Fact]
    public void When_converting_to_bitmap()
    {
        var bytes = GetBytesFromFile("bgra8888bytes.bin");

        using var result = Sut.ConvertBgra24BytesToBitmap(bytes, SKColorType.Bgra8888);

        result.Width.ShouldBe(960);
        result.Height.ShouldBe(160);
    }

    [Fact]
    public void When_converting_to_16bit()
    {
        var bytes = GetBytesFromFile("bgra8888bytes.bin");
        var frame = new byte[2048 * 160];

        Sut.ConvertBgra24ToRgb16(bytes, frame);

        var expected = GetBytesFromFile("push2framebytes.bin");
        frame.ShouldBe(expected);
    }
}