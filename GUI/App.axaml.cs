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

public class App : Application
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
            var services = new ServiceCollection()
                .AddLogging();

            // Event producers and consumers
            services
                .AddSingleton(TimeProvider.System)
                .AddSingleton<IDebugWriter, DebugWriter>()
                .AddSingleton<IPtnshiftFinder, PtnshiftFinder>()
                .AddSingleton<IPush2Usb, Push2Usb>()
                .AddSingleton<IFrameDebugger, FrameDebugger>()
                .AddSingleton<IFrameRateReporter, FrameRateCounter>()
                .AddSingleton<IPreviewRenderer, PreviewRenderer>();

            // Stateless or single-consumer services 
            services
                .AddTransient<ICaptureEventSource, DefaultCaptureEventSource>()
                .AddTransient<IImageConverter, ImageConverter>()
                .AddTransient<IImageSaver, ImageSaver>()
                .AddTransient<ILibUsbWrapper, DefaultLibUsbWrapper>()
                .AddTransient<ISettingsManager, SettingsManager>()
                .AddTransient<IDisplayService, DisplayService>()
                .AddTransient<MainWindowViewModel>();

            if (OperatingSystem.IsMacOS())
            {
                services
                    .AddSingleton<IStreamer, MacStreamer>()
                    .AddSingleton<ICaptureService, MacCaptureService>();
            }
            else if (OperatingSystem.IsWindows())
            {
                services
                    .AddSingleton<IStreamer, WindowsStreamer>()
                    .AddSingleton<ICaptureService, WindowsCaptureService>();
            }

            var provider = services.BuildServiceProvider();
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