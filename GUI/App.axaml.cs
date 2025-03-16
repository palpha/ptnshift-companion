using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Core.Image;
using Core.Settings;
using Core.Usb;
using GUI.ViewModels;
using GUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Core.Capturing;
using Core.Diagnostics;

namespace GUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var provider =
                new ServiceCollection()
                    .AddLogging()
                    .AddSingleton(TimeProvider.System)
                    .AddSingleton<IDebugWriter, DebugWriter>()
                    .AddSingleton<IPtnshiftFinder, PtnshiftFinder>()
#if MACOS
                    .AddSingleton<IStreamer, MacStreamer>()
                    .AddTransient<ICaptureService, MacCaptureService>()
#elif WINDOWS
                    .AddSingleton<IStreamer, WindowsStreamer>()
                    .AddTransient<ICaptureService, WindowsCaptureService>()
#endif
                    .AddTransient<ICaptureEventSource, DefaultCaptureEventSource>()
                    .AddSingleton<IPush2Usb, Push2Usb>()
                    .AddTransient<IImageConverter, ImageConverter>()
                    .AddTransient<IPreviewGenerator, PreviewGenerator>()
                    .AddTransient<IImageSaver, ImageSaver>()
                    .AddTransient<ILibUsbWrapper, DefaultLibUsbWrapper>()
                    .AddTransient<ISettingsManager, SettingsManager>()
                    .AddTransient<IDisplayService, DisplayService>()
                    .AddTransient<MainWindowViewModel>()
                    .BuildServiceProvider();
            var model = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = model,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight
            };

            var saved = false;
            desktop.MainWindow.Closing += async (_, e) =>
            {
                if (saved)
                {
                    return;
                }

                e.Cancel = true;

                if (desktop.MainWindow.DataContext is MainWindowViewModel vm)
                {
                    saved = true;
                    await vm.SaveSettingsAsync();
                }

                desktop.MainWindow.Close();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}