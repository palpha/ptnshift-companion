using Core.Diagnostics;
using SkiaSharp;

namespace Core.Image;

public interface IImageSaver
{
    void SavePngToDisk(SKData data, string? filename = null);
}

public class ImageSaver(IDebugWriter debugWriter) : IImageSaver
{
    private IDebugWriter DebugWriter { get; } = debugWriter;

    public void SavePngToDisk(SKData data, string? filename = null)
    {
        if (filename == null)
        {
            var tmpDir = Path.GetTempPath();
            filename = Path.Combine(tmpDir, $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }

        using var fs = new FileStream(filename, FileMode.Create);
        data.SaveTo(fs);
        DebugWriter.Write($"Frame saved: {filename}\n");
    }
}