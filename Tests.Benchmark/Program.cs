// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Core.Image;
using Microsoft.Extensions.Logging;

var summary = BenchmarkRunner.Run(typeof(Program).Assembly);

namespace Tests.Benchmark
{
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
            Converter.ConvertBgra24ToRgb16(RawBytes.Span, target);
        }

        private byte[] GetBytesFromFile(string fileName) =>
            File.ReadAllBytes(
                Path.Combine("..", "..", "..", "..", "..", "..", "..", "..", "Tests.App", fileName));
    }
}