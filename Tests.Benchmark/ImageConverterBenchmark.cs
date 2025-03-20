using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Core.Image;

namespace Tests.Benchmark;

public class ImageConverterBenchmark
{
    private ImageConverter Converter { get; }
    private Memory<byte> RawBytes { get; }

    public ImageConverterBenchmark()
    {
        Converter = new();
        RawBytes = new(GetBytesFromFile("bgra8888bytes.bin"));
    }

    [Benchmark]
    public void Convert()
    {
        var target = new byte[2048 * 160];
        Converter.ConvertBgra32ToRgb16(RawBytes.Span, target);
    }

    private byte[] GetBytesFromFile(string fileName) =>
        File.ReadAllBytes(
            Path.Combine("..", "..", "..", "..", "..", "..", "..", "..", "Tests.GUI", fileName));
}