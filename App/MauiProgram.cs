using CommunityToolkit.Maui;
using Core.Image;
using Core.Usb;
using Microsoft.Extensions.Logging;
using LiveshiftCompanion.Resources.Fonts;
using LiveshiftCompanion.Services;
using Syncfusion.Maui.Toolkit.Hosting;
using MainPageModel = LiveshiftCompanion.PageModels.MainPageModel;

namespace LiveshiftCompanion;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureSyncfusionToolkit()
            .ConfigureMauiHandlers(handlers => { })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
            });

#if DEBUG
        builder.Logging.AddDebug().SetMinimumLevel(LogLevel.Debug);
        builder.Services.AddLogging(configure => configure.AddDebug().SetMinimumLevel(LogLevel.Debug));
#endif

        // builder.Logging.AddConsole();
        // builder.Services.AddLogging(x => x.AddConsole());

        builder.Services
            .AddSingleton<ModalErrorHandler>()
            .AddSingleton<IStreamer, Streamer>()
            .AddTransient<ICaptureEventSource, DefaultCaptureEventSource>()
            .AddSingleton<IPush2Usb, Push2Usb>()
            .AddTransient<IImageConverter, ImageConverter>()
            .AddTransient<ILibUsbWrapper, DefaultLibUsbWrapper>()
            .AddTransient<MainPageModel>();

        //builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");

        return builder.Build();
    }
}