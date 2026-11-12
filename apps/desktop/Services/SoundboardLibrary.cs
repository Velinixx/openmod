using System;
using System.IO;
using System.Linq;

namespace OpenMod.Desktop.Services;

/// <summary>
/// Soundboard clips, the lazy way: count file names only, never decode. The Rust
/// core does the real work (scan/probe/decode); this just needs to tell the UI
/// how many clips exist and where they live.
/// </summary>
public static class SoundboardLibrary
{
    private static readonly string[] Extensions = [".wav", ".mp3", ".ogg", ".flac", ".m4a", ".aac"];

    /// <summary>Where the user's clips live — config dir first, repo folder second.</summary>
    public static string Dir
    {
        get
        {
            var configured = AppSettings.Load().SoundboardDir;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            var inConfig = Path.Combine(AppSettings.ConfigDir, "soundboards");
            if (Directory.Exists(inConfig))
            {
                return inConfig;
            }

            var inRepo = Path.Combine(PresetLibrary.RepoRoot, "soundboards");
            return Directory.Exists(inRepo) ? inRepo : inConfig;
        }
    }

    /// <summary>Short, readable path for the UI ("~/..." instead of the full home dir).</summary>
    public static string DisplayPath => Tilde(Dir);

    public static int Count()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                return 0;
            }

            return Directory.EnumerateFiles(Dir, "*", SearchOption.TopDirectoryOnly)
                .Count(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// Voicemod's preset folder, if this machine has one. On Linux there never
    /// is one — Voicemod doesn't ship here at all — so this stays honest rather
    /// than showing a fake row.
    /// </summary>
    public static (bool Found, string Path) FindVoicemod()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "Voicemod");
            return (Directory.Exists(path), path);
        }

        // Some people run Voicemod in a Wine prefix; look in the usual place.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var wine = Path.Combine(home, ".wine", "drive_c", "users");
        try
        {
            if (Directory.Exists(wine))
            {
                foreach (var user in Directory.EnumerateDirectories(wine))
                {
                    var candidate = Path.Combine(user, "AppData", "Roaming", "Voicemod");
                    if (Directory.Exists(candidate))
                    {
                        return (true, candidate);
                    }
                }
            }
        }
        catch (Exception)
        {
            // unreadable prefix — treat as absent
        }

        return (false, "not on this machine");
    }

    private static string Tilde(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.Ordinal)
            ? "~" + path[home.Length..]
            : path;
    }
}
