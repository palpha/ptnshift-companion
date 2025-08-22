using Avalonia.Media.Imaging;

namespace Core.Capturing;

public record CapturableWindow(
    int WindowId,
    string ApplicationName,
    string Title,
    Func<CancellationToken, Task<Bitmap?>> LoadThumbnailAsync);

public interface ICapturableWindowService
{
    Task<ICollection<CapturableWindow>> ListAsync();
}
