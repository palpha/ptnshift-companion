using Core.Diagnostics;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Core.Image;

public interface IImageSaver
{
    void SavePngToDisk(SKData data, string? filename = null);
}

public class ImageSaver(ILogger<ImageSaver> logger) : IImageSaver
{
    private ILogger<ImageSaver> Logger { get; } = logger;

    public void SavePngToDisk(SKData data, string? filename = null)
    {
        Logger.LogInformation("Saving frame");

        if (filename == null)
        {
            var tmpDir = Path.GetTempPath();
            filename = Path.Combine(tmpDir, $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }

        using var fs = new FileStream(filename, FileMode.Create);
        data.SaveTo(fs);

        Logger.LogInformation("Frame saved to {Path}", filename);
    }
}
