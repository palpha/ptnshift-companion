using System.Runtime.InteropServices;

namespace Core.Image;

public static class ScreenHelper
{
    public record DisplayInfo(int Id, int Width, int Height, bool IsPrimary)
    {
        public string DisplayName => $"{Id}: {Width}x{Height}, {(IsPrimary ? "Primary" : "Secondary")}";
    }

#if MACCATALYST
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

    public static IEnumerable<DisplayInfo> ListDisplays()
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
#endif

#if !MACCATALYST
    public static IEnumerable<DisplayInfo> ListDisplays()
    {
        return Enumerable.Empty<DisplayInfo>();
    }
#endif
}