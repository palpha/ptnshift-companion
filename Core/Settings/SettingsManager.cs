using System.Text.Json;
using System.Text.Json.Serialization;

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

public class SettingsManager : ISettingsManager
{
    public static readonly string SettingsPath =
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
        if (File.Exists(SettingsPath) == false)
        {
            return new();
        }

        var json = await File.ReadAllTextAsync(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new();
    }

    public async Task SaveAsync(AppSettings settings)
    {
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