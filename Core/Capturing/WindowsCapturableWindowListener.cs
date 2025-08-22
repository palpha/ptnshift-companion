namespace Core.Capturing;

public class WindowsCapturableWindowListener : ICapturableWindowLister
{
    public Task<IEnumerable<ApplicationInfo>> ListApplicationsAsync() => throw new NotImplementedException();
    public Task<IEnumerable<WindowInfo>> ListWindowsAsync() => throw new NotImplementedException();
    public Task<byte[]?> GetThumbnailAsync(int windowId) => throw new NotImplementedException();
}
