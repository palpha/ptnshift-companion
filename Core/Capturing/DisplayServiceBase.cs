using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform;
using Core.Settings;

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
    Screens Screens { set; }
    ObservableCollection<DisplayInfo> AvailableDisplays { get; }
    DisplayInfo? GetDefaultDisplay(AppSettings? settings);
    DisplayInfo? GetDisplay(int? displayId);
}

public abstract class DisplayServiceBase : IDisplayService
{
    private Screens? screens;

    public Screens? Screens
    {
        protected get => screens;
        set
        {
            screens = value;
            if (value == null)
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

                return value.All.FirstOrDefault(screen =>
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
        }
    }

    public ObservableCollection<DisplayInfo> AvailableDisplays { get; } = [];

    public DisplayInfo? GetDefaultDisplay(AppSettings? settings) =>
        GetDisplay(settings?.SelectedDisplayId) ?? AvailableDisplays.FirstOrDefault();

    public DisplayInfo? GetDisplay(int? displayId) =>
        AvailableDisplays.FirstOrDefault(x => x.Id == displayId);

    protected abstract IEnumerable<DisplayInfo> ListDisplays();
}
