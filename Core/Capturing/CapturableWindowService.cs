using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public class CapturableWindowService(
    ILogger<CapturableWindowService> logger,
    ICapturableWindowLister lister)
    : ICapturableWindowService
{
    private ILogger<CapturableWindowService> Logger { get; } = logger;
    private ICapturableWindowLister Lister { get; } = lister;

    public async Task<ICollection<CapturableWindow>> ListAsync()
    {
        var windows = (await Lister.ListWindowsAsync()).ToList();
        Logger.LogDebug("Found {Count} windows", windows.Count);
        var filtered =
            windows
                .Where(x => x is {Width: > 300, Height: > 300})
                .Where(x => x.Kind == WindowKind.Normal)
                .Where(x => x.IsOnScreen)
                .Where(x => x.Alpha > 0.1f)
                .Where(x => (x.ApplicationName == "Dock" && x.Title.StartsWith("Wallpaper")) == false)
                .Where(x => x is not {ApplicationName: "", Title: "Desktop"}
                    and not {ApplicationName: "Wallpaper", Title: "Offscreen Wallpaper Window"}
                    and not {ApplicationName: "WindowManager", Title: "Event Shield Window"}
                    and not {ApplicationName: "Finder", Title: ""}) // TODO: exclusions per OS
                .OrderByDescending(x => x.ApplicationName.StartsWith("Reaktor"))
                .ThenBy(x => x.ApplicationName)
                .ThenBy(x => x.Title)
                .Select(x =>
                    new CapturableWindow(
                        x.WindowId,
                        x.ApplicationName,
                        x.Title,
                        ct => GetThumbnail(x.WindowId, ct))
                ).ToList();
        Logger.LogDebug("Filtered to {Count} windows", filtered.Count);
        return filtered;
    }

    private async Task<Bitmap?> GetThumbnail(int windowId, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Getting thumbnail for window {WindowId}", windowId);

        var thumbnailBytes = await Lister.GetThumbnailAsync(windowId);
        if (thumbnailBytes == null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var thumb = Bitmap.DecodeToWidth(new MemoryStream(thumbnailBytes), 120);
        return thumb;
    }
}
