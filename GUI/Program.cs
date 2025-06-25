using Avalonia;
using Avalonia.Logging;
using System;
using Serilog;

namespace GUI;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Program
{
    private static SerilogSink SerilogSink { get; set; } = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        SerilogSink = new();
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            SerilogSink.Logger.Fatal(ex, "Application crash");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    // ReSharper disable once MemberCanBePrivate.Global
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToSerilog(SerilogSink);
}

public static class LogExtensions
{
    public static AppBuilder LogToSerilog(this AppBuilder builder, SerilogSink sink)
    {
        Logger.Sink = sink;
        Log.Logger = sink.Logger;
        return builder;
    }

    public static Serilog.Events.LogEventLevel ToSerilog(this LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => Serilog.Events.LogEventLevel.Verbose,
            LogEventLevel.Debug => Serilog.Events.LogEventLevel.Debug,
            LogEventLevel.Information => Serilog.Events.LogEventLevel.Information,
            LogEventLevel.Warning => Serilog.Events.LogEventLevel.Warning,
            LogEventLevel.Error => Serilog.Events.LogEventLevel.Error,
            LogEventLevel.Fatal => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
}

public class SerilogSink : ILogSink
{
    public static string LogFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return System.IO.Path.Combine(appData, "PtnshiftCompanion", "ptnshift.log");
        }
    }

    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    public Serilog.Core.Logger Logger { get; } =
        new LoggerConfiguration()
            .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
            .WriteTo.Console(
                outputTemplate: OutputTemplate
            )
            .WriteTo.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                outputTemplate: OutputTemplate
            )
            .MinimumLevel.Override("Layout", Serilog.Events.LogEventLevel.Warning)
            .CreateLogger();

    public bool IsEnabled(LogEventLevel level, string area) =>
        Logger.IsEnabled(level.ToSerilog());

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (IsEnabled(level, area))
        {
            Logger.ForContext("SourceContext", area)
                .Write(level.ToSerilog(), messageTemplate);
        }
    }

    public void Log(
        LogEventLevel level,
        string area,
        object? source,
        string messageTemplate,
        params object?[] propertyValues)
    {
        if (IsEnabled(level, area))
        {
            Logger.ForContext("SourceContext", area)
                .Write(level.ToSerilog(), messageTemplate, propertyValues);
        }
    }
}
