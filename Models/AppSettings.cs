using System.Text.Json;

namespace VoiceToText.Models;

internal sealed class AppSettings
{
    public string Model { get; set; } = "large-v3";
    public string Language { get; set; } = "auto";
    public string InterfaceLanguage { get; set; } =
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
    public string PushToTalkKey { get; set; } = "CapsLock";
    public bool SuppressPushToTalkKey { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool RestoreClipboard { get; set; } = true;

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceToText");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SettingsPath, true);
    }
}
