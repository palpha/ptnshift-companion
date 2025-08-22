namespace Core.Capturing;
public enum WindowKind
{
    Normal,
    Desktop,
    Menubar,
    Dock,
    System,
    Unknown
}

public record ApplicationInfo(
    int ProcessId,
    string Name,
    string? BundleIdentifier);

public record WindowInfo(
    int WindowId,
    int ProcessId,
    string Title,
    string ApplicationName,
    int Width,
    int Height,
    int Layer,
    float Alpha,
    bool IsOnScreen,
    WindowKind Kind);

public interface ICapturableWindowLister
{
    Task<IEnumerable<ApplicationInfo>> ListApplicationsAsync();
    Task<IEnumerable<WindowInfo>> ListWindowsAsync();
    Task<byte[]?> GetThumbnailAsync(int windowId);
}
