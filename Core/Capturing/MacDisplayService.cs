using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Core.Capturing;

public class MacDisplayService : DisplayServiceBase
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [DllImport(CoreGraphicsLib)]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, IntPtr displays, ref uint displayCount);

    [DllImport(CoreGraphicsLib)]
    private static extern uint CGDisplayPixelsWide(uint display);

    [DllImport(CoreGraphicsLib)]
    private static extern uint CGDisplayPixelsHigh(uint display);

    [DllImport(CoreGraphicsLib)]
    private static extern bool CGDisplayIsMain(uint display);

    [DllImport(CoreGraphicsLib)]
    private static extern CGRect CGDisplayBounds(uint display);

    protected override IEnumerable<DisplayInfo> ListDisplays()
    {
        uint displayCount = 0;
        var countResult = CGGetActiveDisplayList(0, IntPtr.Zero, ref displayCount);
        if (countResult != 0)
        {
            yield break;
        }

        var displaysPtr = Marshal.AllocHGlobal((int) (displayCount * sizeof(uint)));
        try
        {
            var listResult = CGGetActiveDisplayList(displayCount, displaysPtr, ref displayCount);
            if (listResult != 0)
            {
                yield break;
            }

            for (var i = 0; i < displayCount; i++)
            {
                var display = (uint) Marshal.ReadInt32(displaysPtr, i * sizeof(uint));
                var width = CGDisplayPixelsWide(display);
                var height = CGDisplayPixelsHigh(display);
                var isMain = CGDisplayIsMain(display);
                var bounds = CGDisplayBounds(display);

                yield return new(
                    (int) display,
                    (int) width,
                    (int) height,
                    isMain,
                    (int) bounds.Origin.X, (int) bounds.Origin.Y);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(displaysPtr);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGPoint
{
    public double X;
    public double Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGSize
{
    public double Width;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGRect
{
    public CGPoint Origin;
    public CGSize Size;
}
