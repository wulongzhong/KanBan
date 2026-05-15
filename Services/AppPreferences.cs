using System;
using System.IO;
using System.Text.Json;
using KanBan.Serialization;

namespace KanBan.Services;

public sealed class AppPreferences
{
    public string? WorkspaceFolder { get; set; }

    public static string PreferencesPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "KanBan", "preferences.json");
        }
    }

    public static AppPreferences Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return new AppPreferences();
            }

            var json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize(json, KanBanJsonContext.Default.AppPreferences) ?? new AppPreferences();
        }
        catch (JsonException)
        {
            return new AppPreferences();
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(PreferencesPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = $"{PreferencesPath}.tmp";
        var json = JsonSerializer.Serialize(this, KanBanJsonContext.Default.AppPreferences);
        File.WriteAllText(tempPath, json);

        if (File.Exists(PreferencesPath))
        {
            File.Move(tempPath, PreferencesPath, overwrite: true);
        }
        else
        {
            File.Move(tempPath, PreferencesPath);
        }
    }
}
