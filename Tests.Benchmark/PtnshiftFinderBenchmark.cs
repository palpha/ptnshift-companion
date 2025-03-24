using BenchmarkDotNet.Attributes;
using Core.Capturing;
using Core.Diagnostics;
using Tests.GUI;

namespace Tests.Benchmark;

public class PtnshiftFinderBenchmark
{
    private static string GetPath(string filename) => Path.Combine("..", "..", "..", "..", filename);

    private PtnshiftFinder Finder { get; } = new(TimeProvider.System);
    private byte[] FullScreenBytes { get; } = PtnshiftFinderTests.ReadTestImage(GetPath("fullscreen.png"));
    private byte[] CorrectCaptureBytes { get; } = PtnshiftFinderTests.ReadTestImage(GetPath("correct-region.png"));
    private byte[] FirstBytes { get; }

    public PtnshiftFinderBenchmark()
    {
        FirstBytes = new Span<byte>(CorrectCaptureBytes)[..16].ToArray();
    }

    [Benchmark]
    public void CheckPixels()
    {
        new Span<byte>(CorrectCaptureBytes).IndexOf(FirstBytes);
    }
}