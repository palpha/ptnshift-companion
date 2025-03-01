
using Foundation;
using ObjCRuntime;
#if MACCATALYST
using UIKit;
#endif

namespace LiveshiftCompanion;

public partial class App
{
    private Window? MainWindow { get; set; }

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        const int Width = 970, Height = 550;

        var effectiveWidth = Width * DeviceDisplay.MainDisplayInfo.Density;
        var effectiveHeight = Height * DeviceDisplay.MainDisplayInfo.Density;

        MainWindow = new(new AppShell())
        {
            Width = effectiveWidth,
            Height = effectiveHeight,
            MinimumWidth = effectiveWidth,
            MinimumHeight = effectiveHeight,
            MaximumWidth = effectiveWidth,
            MaximumHeight = effectiveHeight
        };

#if MACCATALYST
        MainWindow.Created += async (_, _) =>
        {
            await Task.Delay(500); // Give macOS time to register the window
            DisableFullScreenButton();
        };
#endif

        return MainWindow;
    }

#if MACCATALYST
    private void DisableFullScreenButton()
    {
        if (UIDevice.CurrentDevice.UserInterfaceIdiom !=
            UIUserInterfaceIdiom.Pad) return; // MacCatalyst reports as iPad
        var nsWindow = GetNativeMacWindow(MainWindow!);
        if (nsWindow is not null)
        {
            UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                // Modify window properties to disable fullscreen
                SetWindowFlags(nsWindow);
            });
        }
    }

    private static void SetWindowFlags(NSObject nsWindow)
    {
        void ModifyProperty(string property, NSObject obj, int[] set, int[] clear)
        {
            if (obj.ValueForKey(new(property)) is NSNumber value)
            {
                var newValue = (value.Int32Value & ~BitSet(clear)) | BitSet(set);
                obj.SetValueForKey(NSNumber.FromInt32(newValue), new(property));
            }
        }

        int BitSet(int[] bits) => bits.Aggregate(0, (acc, bit) => acc | (1 << bit));

        // Disable resizing and fullscreen
        const int Resizable = 3;
        const int FullScreenPrimary = 7;
        const int FullScreenAuxiliary = 8;
        const int FullScreenNone = 9;

        ModifyProperty("styleMask", nsWindow, [], [Resizable]);
        ModifyProperty("collectionBehavior", nsWindow, [FullScreenNone], [FullScreenPrimary, FullScreenAuxiliary]);
    }

    private static NSObject? GetNativeMacWindow(Window window)
    {
        if (window.Handler?.PlatformView is not UIWindow)
        {
            return null;
        }

        var nsApplication = Runtime.GetNSObject(Class.GetHandle("NSApplication"));
        if (nsApplication is null)
        {
            return null;
        }

        var sharedApp = nsApplication.PerformSelector(new Selector("sharedApplication"));
        var windowsArray = sharedApp.PerformSelector(new Selector("windows"));
        if (windowsArray is NSArray { Count: > 0 } arr)
        {
            Console.WriteLine($"[DEBUG] Found {arr.Count} macOS windows.");
            return arr.GetItem<NSObject>(0); // Return the first NSWindow
        }

        return null;
    }
#endif
}