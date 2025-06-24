using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Core.Settings;

[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

public interface ISettingsManager
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}

public class SettingsManager(ILogger<SettingsManager> logger) : ISettingsManager
{
    private ILogger<SettingsManager> Logger { get; } = logger;

    private static readonly string SettingsPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PtnshiftCompanion",
            "settings.json");

    private static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        WriteIndented = true,
        TypeInfoResolver = AppSettingsJsonContext.Default
    };

    public async Task<AppSettings> LoadAsync()
    {
        Logger.LogInformation("Loading settings from {Path}", SettingsPath);

        if (File.Exists(SettingsPath) == false)
        {
            Logger.LogInformation("Settings file not found");
            return new();
        }

        var json = await File.ReadAllTextAsync(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Logger.LogInformation("Saving settings to {Path}", SettingsPath);

        var directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("Could not locate settings directory.");

        if (Directory.Exists(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }
}
