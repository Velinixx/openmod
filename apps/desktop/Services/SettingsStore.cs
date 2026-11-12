using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenMod.Desktop.Services;

/// <summary>
/// User settings, persisted to <c>$XDG_CONFIG_HOME/openmod/settings.json</c>.
/// Written on every change — it's a handful of fields, and losing a theme
/// choice because the app was killed is worse than a tiny write.
/// </summary>
public sealed class AppSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System";

    /// <summary>Preset name, or <c>"custom"</c> when <see cref="AccentBright"/> is set.</summary>
    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "blush";

    [JsonPropertyName("accentBright")]
    public string? AccentBright { get; set; }

    [JsonPropertyName("accentDeep")]
    public string? AccentDeep { get; set; }

    [JsonPropertyName("matchSystemAccent")]
    public bool MatchSystemAccent { get; set; }

    [JsonPropertyName("monitor")]
    public bool Monitor { get; set; }

    [JsonPropertyName("inputDevice")]
    public string? InputDevice { get; set; }

    [JsonPropertyName("firstRunComplete")]
    public bool FirstRunComplete { get; set; }

    /// <summary>Set by the wizard's import step; null means "use the default folder".</summary>
    [JsonPropertyName("soundboardDir")]
    public string? SoundboardDir { get; set; }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string ConfigDir
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "openmod");
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "openmod");
        }
    }

    public static string FilePath => Path.Combine(ConfigDir, "settings.json");

    /// <summary>Never throws — a broken/missing file just yields defaults.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json)
                       ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // unreadable settings shouldn't block startup
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception)
        {
            // read-only home, full disk, whatever — not worth crashing over
        }
    }
}
