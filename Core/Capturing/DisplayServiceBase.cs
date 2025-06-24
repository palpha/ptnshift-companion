using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Platform;
using Core.Settings;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public record DisplayInfo(
    int Id,
    int Width,
    int Height,
    bool IsPrimary,
    int BoundsX,
    int BoundsY,
    int? DpiX = null,
    int? DpiY = null)
{
    public float ScalingFactor => DpiX / 96.0f ?? 1;
    //public bool IsEnabled => false;//960 * ScalingFactor <= Width / ScalingFactor;

    public override string ToString()
    {
        var dpi = DpiX.HasValue && DpiY.HasValue
            ? DpiX != DpiY
                ? $", {DpiX}x{DpiY} DPI"
                : $", {DpiX} DPI"
            : "";
        return $"{Id}: {Width}x{Height}{dpi}, {(IsPrimary ? "Primary" : "Secondary")}";
    }
}

public interface IDisplayService
{
    ObservableCollection<DisplayInfo> AvailableDisplays { get; }
    void ConfigureScreens(Screens? screens);
    DisplayInfo? GetDefaultDisplay(AppSettings? settings);
    DisplayInfo? GetDisplay(int? displayId);
}

public abstract class DisplayServiceBase(ILogger<DisplayServiceBase> logger) : IDisplayService
{
    private ILogger<DisplayServiceBase> Logger { get; } = logger;

    public void ConfigureScreens(Screens? screens)
    {
        Logger.LogInformation("Configuring screens: {@Screens}", screens);

        if (screens == null)
        {
            return;
        }

        Screen? FindMatchingScreen(int displayId, int? threshold = null)
        {
            var display = GetDisplay(displayId);
            if (display == null)
            {
                return null;
            }

            return screens.All.FirstOrDefault(screen =>
                Math.Abs(screen.Bounds.X - display.BoundsX) <= (threshold ?? 1)
                && Math.Abs(screen.Bounds.Y - display.BoundsY) <= (threshold ?? 1));
        }

        AvailableDisplays.Clear();
        foreach (var display in ListDisplays()
            .OrderBy(x => x.Id)
            .Select(x => x with
            {
                IsPrimary = FindMatchingScreen(x.Id)?.IsPrimary ?? x.IsPrimary,
            })
            .ToList())
        {
            AvailableDisplays.Add(display);
        }

        Logger.LogInformation("Available displays: {@AvailableDisplays}", AvailableDisplays);
    }

    public ObservableCollection<DisplayInfo> AvailableDisplays { get; } = [];

    public DisplayInfo? GetDefaultDisplay(AppSettings? settings) =>
        GetDisplay(settings?.SelectedDisplayId) ?? AvailableDisplays.FirstOrDefault();

    public DisplayInfo? GetDisplay(int? displayId) =>
        AvailableDisplays.FirstOrDefault(x => x.Id == displayId);

    protected abstract IEnumerable<DisplayInfo> ListDisplays();
}
