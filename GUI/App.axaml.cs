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
using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace GUI;

public class App : Application
{
    private IModalWindowFactory ModalWindowFactory { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    [RequiresUnreferencedCode("Uses data validation plugins that may not be preserved in trimming scenarios.")]
#pragma warning disable IL2046
    public override void OnFrameworkInitializationCompleted()
#pragma warning restore IL2046
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var services = new ServiceCollection();

            // Logging
            var serilogLoggerFactory = new SerilogLoggerFactory(Log.Logger);
            services
                .AddSingleton<ILoggerFactory>(serilogLoggerFactory)
                .AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Event producers and consumers
            services
                .AddSingleton(TimeProvider.System)
                .AddSingleton<IPtnshiftFinder, PtnshiftFinder>()
                .AddSingleton<IPush2Usb, Push2Usb>()
                .AddSingleton<IFrameDebugger, FrameDebugger>()
                .AddSingleton<IFrameRateReporter, FrameRateCounter>()
                .AddSingleton<IPreviewRenderer, PreviewRenderer>()
                .AddSingleton<IDiagnosticOutputRenderer, DiagnosticOutputRenderer>()
                .AddSingleton<ICaptureEventSource, DefaultCaptureEventSource>();

            // Stateless or single-consumer services
            services
                .AddTransient<IImageConverter, ImageConverter>()
                .AddTransient<IImageSaver, ImageSaver>()
                .AddTransient<ILibUsbWrapper, DefaultLibUsbWrapper>()
                .AddTransient<ISettingsManager, SettingsManager>()
                .AddTransient<MainWindow>()
                .AddTransient<MainWindowViewModel>()
                .AddTransient<AboutWindow>()
                .AddTransient<AboutWindowViewModel>()
                .AddTransient<WindowSelectionView>()
                .AddTransient<WindowSelectionViewModel>()
                .AddTransient<ICapturableWindowService, CapturableWindowService>();

            if (OperatingSystem.IsMacOS())
            {
                services
                    .AddSingleton<IStreamer, MacStreamer>()
                    .AddSingleton<ICaptureService, MacCaptureService>()
                    .AddSingleton<IDisplayService, MacDisplayService>()
                    .AddSingleton<ICapturableWindowLister, MacCapturableWindowLister>();
            }
            else if (OperatingSystem.IsWindows())
            {
                services
                    .AddSingleton<IStreamer, WindowsStreamer>()
                    .AddSingleton<ICaptureService, WindowsCaptureService>()
                    .AddSingleton<IDisplayService, WindowsDisplayService>()
                    .AddSingleton<ICapturableWindowLister, WindowsCapturableWindowListener>();
            }

            // Register the modal window factory
            services.AddSingleton<IModalWindowFactory, ModalWindowFactory>();

            var provider = services.BuildServiceProvider();
            ModalWindowFactory = provider.GetRequiredService<IModalWindowFactory>();

            desktop.MainWindow = ModalWindowFactory.Create<MainWindow, MainWindowViewModel>();
            desktop.MainWindow.CanResize = false;
            desktop.MainWindow.SizeToContent = SizeToContent.WidthAndHeight;

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

    [RequiresUnreferencedCode("Calls Avalonia.Data.Core.Plugins.BindingPlugins.DataValidators")]
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void WithMainWindow(Action<MainWindow> action)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime {MainWindow: MainWindow mainWindow})
        {
            action(mainWindow);
        }
    }

    private void WithMainWindowViewModel(Action<MainWindowViewModel> action) =>
        WithMainWindow(x => action(x.ViewModel));

    private void About_OnClick(object? sender, EventArgs e) =>
        WithMainWindow(x => ModalWindowFactory.Create<AboutWindow, AboutWindowViewModel>().ShowDialog(x));

    private void Reset_OnClick(object? sender, EventArgs e) =>
        WithMainWindowViewModel(x => Dispatcher.UIThread.InvokeAsync(x.ExecuteEmergencyResetAsync));

    private void Log_OnClick(object? sender, EventArgs e) =>
        WithMainWindowViewModel(x => x.ExecuteOpenLogFile());
}
