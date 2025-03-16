using Core.Settings;
using System.Collections.ObjectModel;
#if MACOS
using System.Runtime.InteropServices;
#endif

namespace Core.Capturing;

public record DisplayInfo(int Id, int Width, int Height, bool IsPrimary)
{
    public override string ToString() => $"{Id}: {Width}x{Height}, {(IsPrimary ? "Primary" : "Secondary")}";
}

public interface IDisplayService
{
    ObservableCollection<DisplayInfo> AvailableDisplays { get; }
    DisplayInfo? GetDefaultDisplay(AppSettings? settings);
    DisplayInfo? GetDisplay(int? displayId);
}

public class DisplayService : IDisplayService
{
    public ObservableCollection<DisplayInfo> AvailableDisplays { get; }

    public DisplayService() =>
        AvailableDisplays = new(ListDisplays()
            .OrderBy(x => x.Id)
            .ToList());

    public DisplayInfo? GetDefaultDisplay(AppSettings? settings) =>
        GetDisplay(settings?.SelectedDisplayId) ?? AvailableDisplays.FirstOrDefault();

    public DisplayInfo? GetDisplay(int? displayId) =>
        AvailableDisplays.FirstOrDefault(x => x.Id == displayId);

#if MACOS
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // Import CGGetActiveDisplayList
    [DllImport(CoreGraphicsLib)]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, IntPtr displays, ref uint displayCount);

    // Import CGDisplayPixelsWide
    [DllImport(CoreGraphicsLib)]
    private static extern uint CGDisplayPixelsWide(uint display);

    // Import CGDisplayPixelsHigh
    [DllImport(CoreGraphicsLib)]
    private static extern uint CGDisplayPixelsHigh(uint display);

    // Import CGDisplayIsMain
    [DllImport(CoreGraphicsLib)]
    private static extern bool CGDisplayIsMain(uint display);

    private static IEnumerable<DisplayInfo> ListDisplays()
    {
        uint displayCount = 0;
        var countResult = CGGetActiveDisplayList(0, IntPtr.Zero, ref displayCount);
        if (countResult != 0)
        {
            yield break;
        }

        var displaysPtr = Marshal.AllocHGlobal((int)(displayCount * sizeof(uint)));
        try
        {
            var listResult = CGGetActiveDisplayList(displayCount, displaysPtr, ref displayCount);
            if (listResult != 0)
            {
                yield break;
            }

            for (var i = 0; i < displayCount; i++)
            {
                var display = (uint)Marshal.ReadInt32(displaysPtr, i * sizeof(uint));
                var width = CGDisplayPixelsWide(display);
                var height = CGDisplayPixelsHigh(display);
                var isMain = CGDisplayIsMain(display);

                yield return new(
                    (int)display,
                    (int)width,
                    (int)height,
                    isMain);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(displaysPtr);
        }
    }
#elif WINDOWS
    private static IEnumerable<DisplayInfo> ListDisplays()
    {
        var displays = new WinScreenStreamLib.DisplayInfo[10];
        var count = WinScreenStreamLib.GetActiveDisplays(displays, displays.Length);
        for (var i = 0; i < count; i++)
        {
            var display = displays[i];
            yield return new(display.id, display.width, display.height, display.isPrimary);
        }
    }
#else
    private static IEnumerable<DisplayInfo> ListDisplays()
    {
        throw new NotImplementedException();
    }
#endif
}