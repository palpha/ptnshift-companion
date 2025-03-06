using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GUI.ViewModels;

[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

public static class SettingsManager
{
    public static readonly string SettingsPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LiveshiftCompanion",
            "settings.json");

    private static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        WriteIndented = true,
        TypeInfoResolver = AppSettingsJsonContext.Default
    };

    public static async Task<AppSettings> LoadAsync()
    {
        if (File.Exists(SettingsPath) == false)
        {
            return new();
        }

        var json = await File.ReadAllTextAsync(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new();
    }

    public static async Task SaveAsync(AppSettings settings)
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