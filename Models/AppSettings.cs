using System.Text.Json;

namespace VoiceToText.Models;

internal sealed class AppSettings
{
    public const int CurrentSettingsVersion = 2;

    public int SettingsVersion { get; set; } = CurrentSettingsVersion;
    public string Model { get; set; } = "large-v3";
    public string Language { get; set; } = "auto";
    public string InterfaceLanguage { get; set; } =
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
    public string PushToTalkKey { get; set; } = "F8";
    public string MicrophoneId { get; set; } = "";
    public int MicrophoneSensitivity { get; set; } = 250;
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
            {
                var json = File.ReadAllText(SettingsPath);
                using var document = JsonDocument.Parse(json);
                var hasVersion = document.RootElement.TryGetProperty(nameof(SettingsVersion), out _);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                if (!hasVersion)
                {
                    settings.MigrateLegacySettings();
                    try { settings.Save(); } catch { }
                }
                return settings;
            }
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

    internal void MigrateLegacySettings()
    {
        // Caps Lock used to be the default and was globally swallowed, making the
        // normal capitalization toggle unavailable while VoiceToText was running.
        if (PushToTalkKey.Equals("CapsLock", StringComparison.OrdinalIgnoreCase))
            PushToTalkKey = "F8";
        SettingsVersion = CurrentSettingsVersion;
    }
}
