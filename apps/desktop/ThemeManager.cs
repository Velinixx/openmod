using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using OpenMod.Desktop.Services;

namespace OpenMod.Desktop;

public enum ThemeMode
{
    System,
    Light,
    Dark,
    Amoled,
}

/// <summary>
/// Applies palettes and accent tokens at runtime. Screens only ever bind to
/// resource keys (BgBrush, AccentBrush, ...) so a theme switch is one swap.
///
/// Accent state lives here (not in the view model) because the palette swap and
/// the accent override have to happen together: accent-colored *text* uses the
/// deep tone on light backgrounds and the bright tone on dark ones.
/// </summary>
public static class ThemeManager
{
    public sealed record AccentPreset(string Name, Color Bright, Color Deep);

    public static readonly AccentPreset[] Accents =
    [
        new("blush", Color.Parse("#FF99CC"), Color.Parse("#E91E63")),
        new("mint", Color.Parse("#7FE0C3"), Color.Parse("#0FA97F")),
        new("sky", Color.Parse("#8CC8FF"), Color.Parse("#2E7DD1")),
        new("violet", Color.Parse("#C3A6FF"), Color.Parse("#7C4DFF")),
        new("amber", Color.Parse("#FFCF7A"), Color.Parse("#E08C00")),
        new("coral", Color.Parse("#FFA08C"), Color.Parse("#E05A3F")),
    ];

    public static ThemeMode Mode { get; private set; } = ThemeMode.System;

    /// <summary>Preset name, <c>"custom"</c>, or <c>"system"</c>.</summary>
    public static string AccentName { get; private set; } = "blush";

    public static Color AccentBright { get; private set; } = Accents[0].Bright;
    public static Color AccentDeep { get; private set; } = Accents[0].Deep;
    public static bool MatchSystemAccent { get; private set; }

    /// <summary>Raised after a palette/accent swap so views can refresh selection state.</summary>
    public static event Action? Changed;

    private static bool _watchingSystem;

    // ---------------------------------------------------------------- startup

    /// <summary>Apply a saved settings file.</summary>
    public static void ApplySettings(AppSettings settings)
    {
        Mode = Enum.TryParse<ThemeMode>(settings.Theme, true, out var parsed) ? parsed : ThemeMode.System;
        MatchSystemAccent = settings.MatchSystemAccent;

        if (MatchSystemAccent && SystemAccent.TryRead(out var sysBright, out var sysDeep))
        {
            AccentName = "system";
            AccentBright = sysBright;
            AccentDeep = sysDeep;
        }
        else if (string.Equals(settings.Accent, "custom", StringComparison.OrdinalIgnoreCase)
                 && TryParse(settings.AccentBright, out var customBright)
                 && TryParse(settings.AccentDeep, out var customDeep))
        {
            AccentName = "custom";
            AccentBright = customBright;
            AccentDeep = customDeep;
        }
        else if (Accents.FirstOrDefault(a => a.Name.Equals(settings.Accent, StringComparison.OrdinalIgnoreCase)) is { } preset)
        {
            AccentName = preset.Name;
            AccentBright = preset.Bright;
            AccentDeep = preset.Deep;
        }

        WatchSystemTheme();
        ApplyInternal();
    }

    /// <summary>Quick apply for the screenshot harness: mode and/or accent name.</summary>
    public static void Apply(string? mode = null, string? accent = null)
    {
        if (mode is not null)
        {
            Mode = Enum.TryParse<ThemeMode>(mode, true, out var parsed) ? parsed : ThemeMode.System;
        }

        if (accent is not null
            && Accents.FirstOrDefault(a => a.Name.Equals(accent, StringComparison.OrdinalIgnoreCase)) is { } preset)
        {
            AccentName = preset.Name;
            AccentBright = preset.Bright;
            AccentDeep = preset.Deep;
            MatchSystemAccent = false;
        }

        WatchSystemTheme();
        ApplyInternal();
    }

    // ----------------------------------------------------------------- setters

    public static void SetMode(ThemeMode mode)
    {
        Mode = mode;
        ApplyInternal();
    }

    public static void SetAccentPreset(string name)
    {
        if (Accents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is not { } preset)
        {
            return;
        }

        AccentName = preset.Name;
        AccentBright = preset.Bright;
        AccentDeep = preset.Deep;
        MatchSystemAccent = false;
        ApplyInternal();
    }

    /// <summary>Custom colour: the "bright" tone is derived from the picked colour.</summary>
    public static void SetCustomAccent(Color picked)
    {
        AccentName = "custom";
        AccentBright = SystemAccent.Lighten(picked, 0.25);
        AccentDeep = SystemAccent.Darken(picked, 0.15);
        MatchSystemAccent = false;
        ApplyInternal();
    }

    public static bool SetMatchSystemAccent(bool enabled)
    {
        if (enabled)
        {
            if (!SystemAccent.TryRead(out var bright, out var deep))
            {
                // portal not available — don't pretend we're following it
                MatchSystemAccent = false;
                ApplyInternal();
                return false;
            }

            MatchSystemAccent = true;
            AccentName = "system";
            AccentBright = bright;
            AccentDeep = deep;
        }
        else
        {
            MatchSystemAccent = false;
        }

        ApplyInternal();
        return true;
    }

    /// <summary>Snapshot for persistence.</summary>
    public static void WriteTo(AppSettings settings)
    {
        settings.Theme = Mode.ToString();
        settings.MatchSystemAccent = MatchSystemAccent;
        settings.Accent = AccentName;

        if (AccentName == "custom")
        {
            settings.AccentBright = ToHex(AccentBright);
            settings.AccentDeep = ToHex(AccentDeep);
        }
        else
        {
            settings.AccentBright = null;
            settings.AccentDeep = null;
        }
    }

    // ------------------------------------------------------------------ internals

    private static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        return Color.TryParse(hex, out color);
    }

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static void WatchSystemTheme()
    {
        if (_watchingSystem || Application.Current is not { } app)
        {
            return;
        }

        _watchingSystem = true;
        app.ActualThemeVariantChanged += (_, _) =>
        {
            if (Mode == ThemeMode.System)
            {
                ApplyInternal();
            }
        };
    }

    private static void ApplyInternal()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var isLight = Mode == ThemeMode.Light
            || (Mode == ThemeMode.System && app.ActualThemeVariant == ThemeVariant.Light);

        // Fluent controls (scrollbars, text selection) follow the same mode
        app.RequestedThemeVariant = Mode == ThemeMode.System
            ? ThemeVariant.Default
            : isLight ? ThemeVariant.Light : ThemeVariant.Dark;

        var palette = isLight ? "Light" : Mode == ThemeMode.Amoled ? "Amoled" : "Dark";
        var uri = new Uri($"avares://OpenMod.Desktop/Themes/{palette}.axaml");
        var include = new ResourceInclude(uri) { Source = uri };

        if (app.Resources.MergedDictionaries.Count > 0)
        {
            app.Resources.MergedDictionaries[0] = include;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(include);
        }

        ApplyAccent(app, isLight);
        Changed?.Invoke();
    }

    private static void ApplyAccent(Application app, bool isLight)
    {
        var (bright, deep) = (AccentBright, AccentDeep);
        var r = app.Resources;

        r["AccentBrush"] = new SolidColorBrush(bright);
        r["AccentDeepBrush"] = new SolidColorBrush(deep);
        // accent-colored *text* needs the deep tone on light backgrounds
        r["AccentTextBrush"] = new SolidColorBrush(isLight ? deep : bright);
        r["AccentSoftBrush"] = new SolidColorBrush(bright, 0.10);
        r["AccentSoftStrongBrush"] = new SolidColorBrush(bright, 0.20);
        r["AccentBorderBrush"] = new SolidColorBrush(deep, 0.40);
        r["AccentGradientBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(bright, 0),
                new GradientStop(deep, 1),
            },
        };
        r["AccentMeterBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(bright, 0),
                new GradientStop(deep, 1),
            },
        };
        r["GlowShadow"] = BoxShadows.Parse($"0 0 26 0 {WithAlpha(isLight ? deep : bright, 0x55)}");
    }

    private static string WithAlpha(Color color, byte alpha) =>
        $"#{alpha:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
}
