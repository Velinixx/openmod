using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenMod.Desktop.Models;

namespace OpenMod.Desktop.Services;

/// <summary>
/// Presets are lazy: startup only scans file names (cheap), the JSON body is
/// parsed on demand and cached. Keeps launch instant even with hundreds of
/// community presets.
/// </summary>
public static class PresetLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Dictionary<string, PresetData> Cache = [];

    public static string RepoRoot { get; } = FindRoot();

    public static string EffectsDir => Path.Combine(RepoRoot, "effects");

    public static IReadOnlyList<string> Scan()
    {
        try
        {
            return Directory.EnumerateFiles(EffectsDir, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static PresetData Load(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        PresetData data;
        try
        {
            data = JsonSerializer.Deserialize<PresetData>(File.ReadAllText(path), JsonOptions) ?? new PresetData();
        }
        catch (Exception)
        {
            data = new PresetData();
        }

        Cache[path] = data;
        return data;
    }

    /// <summary>
    /// Mirrors crates/core: the granular pitch shifter costs GRAIN * ratio
    /// samples of algorithmic latency; gain is free.
    /// </summary>
    public static double LatencyMs(PresetData preset, int sampleRate = 44100)
    {
        double samples = 0;
        foreach (var effect in preset.Effects)
        {
            if (effect.Type == "pitch_shift")
            {
                effect.Params.TryGetValue("semitones", out var semitones);
                var ratio = Math.Clamp(Math.Pow(2, semitones / 12.0), 0.25, 4.0);
                samples += 1024 * ratio;
            }
        }

        return samples / sampleRate * 1000.0;
    }

    public static string Chip(PresetEffect effect) => effect.Type switch
    {
        "pitch_shift" => $"pitch {Format(effect.Params.GetValueOrDefault("semitones"))}",
        "gain" => $"gain {Format(effect.Params.GetValueOrDefault("gain_db"))}",
        _ => effect.Type,
    };

    public static string Format(double value) => value.ToString("+0.0;-0.0;0.0");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "effects")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
