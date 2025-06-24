using System.Runtime.InteropServices;
using Core.Capturing;
using Core.Diagnostics;
using Core.Image;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;
using SkiaSharp;

namespace Tests.GUI;

public class PtnshiftFinderTests
{
    private FakeTimeProvider TimeProviderMock { get; } = new();

    private PtnshiftFinder Sut { get; }

    public PtnshiftFinderTests() =>
        Sut = new(TimeProviderMock);

    [Fact]
    public void When_correct_region_captured()
    {
        var lost = false;
        IPtnshiftFinder.Location? result = null;
        Sut.LocationLost += () => lost = true;
        Sut.LocationFound += x => result = x;

        var fullScreen = ReadTestImage("fullscreen.png");
        Sut.OnFullScreenCapture(3008, fullScreen);
        var region = ReadTestImage("correct-region.png");
        Sut.OnRegionCapture(region);

        lost.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("offset-region.png")]
    [InlineData("invalid-region.png")]
    public void When_offset_region_captured(string filename)
    {
        var lost = false;
        IPtnshiftFinder.Location? result = null;
        Sut.LocationLost += () => lost = true;
        Sut.LocationFound += x => result = x;

        var fullScreen = ReadTestImage("fullscreen.png");
        Sut.OnFullScreenCapture(3008, fullScreen);
        var region = ReadTestImage(filename);
        Sut.OnRegionCapture(region);
        TimeProviderMock.Advance(TimeSpan.FromMilliseconds(600));

        lost.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.X.ShouldBe(500);
        result.Y.ShouldBe(500);
    }

    [Fact]
    public void When_invalid_region_captured_and_no_hit()
    {
        var lost = false;
        IPtnshiftFinder.Location? result = null;
        Sut.LocationLost += () => lost = true;

        var fullScreen = ReadTestImage("fullscreen-no-hit.png");
        Sut.OnFullScreenCapture(3008, fullScreen);
        var region = ReadTestImage("invalid-region.png");
        Sut.OnRegionCapture(region);
        TimeProviderMock.Advance(TimeSpan.FromMilliseconds(600));

        Sut.LastLocationCheckTimestamp.ShouldBeGreaterThan(0);
        lost.ShouldBeTrue();
        result.ShouldBeNull();
    }

    // ReSharper disable once UnusedMember.Local
    private static void WriteTestImages()
    {
        void Write(string filename, int width, int height, int posX, int posY)
        {
            var bytes = ImageGenerator.GenerateTestBitmap(width, height, posX, posY);
            var converter = new ImageConverter();
            var data = converter.ConvertToData(bytes, width, height);
            var saver = new ImageSaver(Mock.Of<ILogger<ImageSaver>>());
            var testProjectDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..");
            saver.SavePngToDisk(data, Path.Combine(testProjectDirectory, filename));
        }

        (string, int, int, int, int)[] images =
        [
            ("fullscreen.png", 3008, 1692, 500, 500),
            ("fullscreen-no-hit.png", 3008, 1692, -1, -1),
            ("correct-region.png", 960, 161, 0, 0),
            ("offset-region.png", 960, 161, 10, 10),
            ("invalid-region.png", 960, 161, -1, -1),
        ];

        foreach (var (filename, width, height, posX, posY) in images)
        {
            Write(filename, width, height, posX, posY);
        }
    }

    public static byte[] ReadTestImage(string filename)
    {
        using var stream = File.OpenRead(filename);
        var bitmap = SKBitmap.Decode(stream);
        var byteCount = bitmap.ByteCount;
        var rawPixels = new byte[byteCount];
        Marshal.Copy(bitmap.GetPixels(), rawPixels, 0, byteCount);
        return rawPixels;
    }
}
