using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

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
    public static AccentPreset Accent { get; private set; } = Accents[0];

    public static event Action? Changed;

    private static bool _watchingSystem;

    public static void Apply(string? mode = null, string? accent = null)
    {
        if (mode is not null)
        {
            Mode = Enum.TryParse<ThemeMode>(mode, true, out var parsed) ? parsed : ThemeMode.System;
        }

        if (accent is not null)
        {
            var match = Accents.FirstOrDefault(a => a.Name.Equals(accent, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                Accent = match;
            }
        }

        WatchSystemTheme();
        ApplyInternal();
    }

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
        var (bright, deep) = (Accent.Bright, Accent.Deep);
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
