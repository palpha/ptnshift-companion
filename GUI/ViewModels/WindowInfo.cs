using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GUI.ViewModels;

public class WindowInfo(
    int windowId,
    string? title,
    string? applicationName,
    Func<CancellationToken, Task<Bitmap?>> loadThumbnailAsync)
    : ObservableObject
{
    private Bitmap? thumbnail;
    private bool isThumbnailLoading;

    public int WindowId { get; } = windowId;
    private string? title = title;
    private string? applicationName = applicationName;

    public string? Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public string? ApplicationName
    {
        get => applicationName;
        private set => SetProperty(ref applicationName, value);
    }

    public void UpdateProperties(string? newTitle, string? newApplicationName)
    {
        Title = newTitle;
        ApplicationName = newApplicationName;
    }

    public Bitmap? Thumbnail
    {
        get => thumbnail;
        private set => SetProperty(ref thumbnail, value);
    }

    public bool IsThumbnailLoading
    {
        get => isThumbnailLoading;
        private set => SetProperty(ref isThumbnailLoading, value);
    }

    private Func<CancellationToken, Task<Bitmap?>> LoadThumbnailAsync { get; } = loadThumbnailAsync;
    private CancellationTokenSource? ThumbnailCts { get; set; }

    public async void StartThumbnailLoad()
    {
        try
        {
            if (ThumbnailCts != null)
            {
                await ThumbnailCts.CancelAsync();
            }

            ThumbnailCts = new();
            IsThumbnailLoading = true;

            try
            {
                Thumbnail = await LoadThumbnailAsync(ThumbnailCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            IsThumbnailLoading = false;
        }
    }

    public void CancelThumbnailLoad()
    {
        ThumbnailCts?.Cancel();
        IsThumbnailLoading = false;
    }
}
