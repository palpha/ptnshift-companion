namespace Core.Capturing;

public class WindowsDisplayService : DisplayServiceBase
{
    protected override IEnumerable<DisplayInfo> ListDisplays()
    {
        var displays = new WinScreenStreamLib.DisplayInfo[10];
        var count = WinScreenStreamLib.GetActiveDisplays(displays, displays.Length);
        for (var i = 0; i < count; i++)
        {
            var display = displays[i];
            yield return new(
                display.id,
                display.width, display.height,
                display.isPrimary,
                display.left, display.top,
                (int) display.dpiX, (int) display.dpiY);
        }
    }
}
