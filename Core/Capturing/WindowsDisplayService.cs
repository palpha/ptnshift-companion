using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public class WindowsDisplayService(ILogger<WindowsDisplayService> logger) : DisplayServiceBase(logger)
{
    private ILogger<WindowsDisplayService> Logger { get; } = logger;

    protected override IEnumerable<DisplayInfo> ListDisplays()
    {
        Logger.LogInformation("Listing displays");

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

        Logger.LogInformation("Yielded {Count} displays", count);
    }
}
