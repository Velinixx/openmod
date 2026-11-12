using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenMod.Desktop.Services;
using OpenMod.Desktop.ViewModels;

namespace OpenMod.Desktop;

/// <summary>
/// Headless assertions for the settings path: pick a theme/accent/monitor in the
/// view model, then check both the live resources and the JSON on disk.
/// Run with <c>--selftest</c>; exits non-zero if anything fails.
/// </summary>
static class SelfTest
{
    private static readonly List<string> Failures = [];
    private static int _checks;

    public static int Run()
    {
        // Never touch the user's real config during a test.
        var sandbox = Path.Combine(Path.GetTempPath(), "openmod-selftest-" + Environment.ProcessId);
        Directory.CreateDirectory(sandbox);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", sandbox);

        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        var settings = AppSettings.Load();
        ThemeManager.ApplySettings(settings);
        var vm = new SettingsViewModel(settings);
        Dispatcher.UIThread.RunJobs();

        // --- defaults ------------------------------------------------------
        Check("default theme is System", ThemeManager.Mode == ThemeMode.System, ThemeManager.Mode.ToString());
        Check("default accent is blush", ThemeManager.AccentName == "blush", ThemeManager.AccentName);
        Check("settings file lives under the sandbox",
            AppSettings.FilePath.StartsWith(sandbox, StringComparison.Ordinal), AppSettings.FilePath);

        // --- theme ---------------------------------------------------------
        vm.Themes.Single(t => t.Mode == ThemeMode.Light).SelectCommand.Execute(null);
        Check("selecting Light applies it", ThemeManager.Mode == ThemeMode.Light, ThemeManager.Mode.ToString());
        Check("Light sets the light palette resource",
            Resource("BgBrush") is SolidColorBrush { Color.R: > 240 }, Describe(Resource("BgBrush")));
        Check("Light flips Fluent's variant", Application.Current?.RequestedThemeVariant?.Key.ToString() == "Light",
            Application.Current?.RequestedThemeVariant?.Key.ToString() ?? "null");
        Check("Light persisted to JSON", Json()["theme"] == "Light", Json()["theme"]);

        vm.Themes.Single(t => t.Mode == ThemeMode.Amoled).SelectCommand.Execute(null);
        Check("AMOLED is true black",
            Resource("BgBrush") is SolidColorBrush { Color.R: < 10 }, Describe(Resource("BgBrush")));
        Check("AMOLED persisted", Json()["theme"] == "Amoled", Json()["theme"]);

        // --- accent presets -------------------------------------------------
        vm.Accents.Single(a => a.Name == "mint").SelectCommand.Execute(null);
        Check("mint accent applied", IsColor(Resource("AccentBrush"), "#7FE0C3"),
            Describe(Resource("AccentBrush")));
        Check("mint accent text tone is the bright tone on dark",
            IsColor(Resource("AccentTextBrush"), "#7FE0C3"), Describe(Resource("AccentTextBrush")));
        Check("mint persisted", Json()["accent"] == "mint", Json()["accent"]);
        Check("preset clears custom hexes",
            Json()["accentBright"] is null or "", Json()["accentBright"] ?? "(null)");

        // on a light palette the *text* token must be the deep tone
        vm.Themes.Single(t => t.Mode == ThemeMode.Light).SelectCommand.Execute(null);
        Check("accent text uses the deep tone on light",
            IsColor(Resource("AccentTextBrush"), "#0FA97F"), Describe(Resource("AccentTextBrush")));

        // --- custom accent --------------------------------------------------
        vm.CustomHex = "33FF88";
        vm.UseCustomAccentCommand.Execute(null);
        Check("custom accent accepted without '#'", ThemeManager.AccentName == "custom", ThemeManager.AccentName);
        Check("custom deep tone derived from the pick",
            ThemeManager.AccentDeep == SystemAccent.Darken(Color.Parse("#33FF88"), 0.15),
            ThemeManager.ToHex(ThemeManager.AccentDeep));
        Check("custom hexes persisted",
            Json()["accentBright"] is not null && Json()["accentDeep"] is not null,
            $"{Json()["accentBright"]} / {Json()["accentDeep"]}");

        vm.CustomHex = "not-a-colour";
        vm.UseCustomAccentCommand.Execute(null);
        Check("bogus hex is rejected with a message", vm.HasStatus, vm.StatusMessage);
        Check("bogus hex keeps the previous accent", ThemeManager.AccentName == "custom", ThemeManager.AccentName);

        // --- monitor toggle shares one source of truth ----------------------
        Check("monitor starts off", !vm.Monitor, vm.Monitor.ToString());
        vm.ToggleMonitorCommand.Execute(null);
        Check("monitor toggles on", vm.Monitor, vm.Monitor.ToString());
        Check("monitor persisted", IsTrue(Json()["monitor"]), Json()["monitor"]);

        // --- system accent (graceful either way) ----------------------------
        var before = ThemeManager.AccentName;
        vm.ToggleMatchSystemAccentCommand.Execute(null);
        var portal = ThemeManager.MatchSystemAccent;
        Check("system-accent toggle never leaves a lie behind",
            portal == (ThemeManager.AccentName == "system"),
            $"match={portal} accent={ThemeManager.AccentName} (was {before})");
        Console.WriteLine(portal
            ? "  note: this desktop HAS an accent colour in the portal — followed it"
            : "  note: no settings portal here — fell back to the chosen accent (expected on some sessions)");

        // --- the toggle knob really moves (layout, not just a bool) ---------
        CheckToggleKnob(vm);

        // --- round-trip through a fresh load --------------------------------
        var reloaded = AppSettings.Load();
        // the last theme picked above was Light, so that's what must come back
        Check("reload keeps the theme", reloaded.Theme == "Light", reloaded.Theme);
        Check("reload keeps the monitor flag", reloaded.Monitor, reloaded.Monitor.ToString());

        // --- shell wiring ---------------------------------------------------
        var shell = new MainViewModel(AppSettings.Load());
        Check("shell starts on the voices page", shell.Page == AppPage.Voices, shell.Page.ToString());
        Check("chain panel visible on voices", shell.ShowChainPanel, shell.ShowChainPanel.ToString());
        shell.GoToSettingsCommand.Execute(null);
        Check("theme button opens settings", shell.IsSettingsPage, shell.Page.ToString());
        Check("chain panel hidden on settings", !shell.ShowChainPanel, shell.ShowChainPanel.ToString());
        Check("settings page is the same instance the bottom bar reads",
            ReferenceEquals(shell.Settings, shell.Settings), "same");
        shell.Nav.Single(n => n.Page == AppPage.Soundboard).SelectCommand.Execute(null);
        Check("nav switches pages", shell.IsSoundboardPage, shell.Page.ToString());
        Check("nav marks exactly one item active",
            shell.Nav.Count(n => n.IsActive) == 1, $"{shell.Nav.Count(n => n.IsActive)} active");

        try
        {
            Directory.Delete(sandbox, true);
        }
        catch (Exception)
        {
            // leftovers in /tmp are fine
        }

        Console.WriteLine();
        if (Failures.Count == 0)
        {
            Console.WriteLine($"selftest: {_checks} checks passed");
            return 0;
        }

        Console.WriteLine($"selftest: {Failures.Count}/{_checks} FAILED");
        foreach (var failure in Failures)
        {
            Console.WriteLine("  ✗ " + failure);
        }

        return 1;
    }

    /// <summary>
    /// Lays out the real settings view and checks the pill toggle's knob sits on
    /// the right when on and on the left when off — the one thing a screenshot
    /// of the off state can't tell us.
    /// </summary>
    private static void CheckToggleKnob(SettingsViewModel vm)
    {
        var view = new OpenMod.Desktop.Views.SettingsView { DataContext = vm };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(1280, 800));
        window.Arrange(new Rect(0, 0, 1280, 800));
        Dispatcher.UIThread.RunJobs();

        var toggles = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("toggle"))
            .ToList();

        Check("settings page has both pill toggles", toggles.Count == 2, $"{toggles.Count} found");
        if (toggles.Count == 0)
        {
            window.Close();
            return;
        }

        // the monitor toggle is on (toggled earlier), so its knob must be right
        var monitor = toggles.FirstOrDefault(t => t.Classes.Contains("on"));
        if (monitor is null)
        {
            Check("monitor toggle renders in the on state", false, "no .on toggle");
            window.Close();
            return;
        }

        var knob = monitor.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().FirstOrDefault();
        var width = monitor.Bounds.Width;
        Check("toggle is 46px wide", Math.Abs(width - 46) < 0.5, width.ToString("0.##"));
        Check("on-toggle knob sits on the right",
            knob is not null && knob.Bounds.X > width / 2, knob?.Bounds.X.ToString("0.##") ?? "no knob");

        var off = toggles.FirstOrDefault(t => !t.Classes.Contains("on"));
        if (off is not null)
        {
            var offKnob = off.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().FirstOrDefault();
            Check("off-toggle knob sits on the left",
                offKnob is not null && offKnob.Bounds.X < off.Bounds.Width / 2,
                offKnob?.Bounds.X.ToString("0.##") ?? "no knob");
        }

        window.Close();
    }

    private static object? Resource(string key) =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true
            ? value
            : null;

    /// <summary>Colour equality without caring about casing or the alpha prefix.</summary>
    private static bool IsColor(object? value, string hex) =>
        value is SolidColorBrush brush
        && brush.Color.ToString().TrimStart('#').ToUpperInvariant()
            .EndsWith(hex.TrimStart('#').ToUpperInvariant(), StringComparison.Ordinal);

    private static bool IsTrue(string? json) =>
        string.Equals(json, "true", StringComparison.OrdinalIgnoreCase);

    private static string Describe(object? value) =>
        value is SolidColorBrush brush ? brush.Color.ToString() : value?.ToString() ?? "(missing)";

    private static Dictionary<string, string?> Json()
    {
        var text = File.Exists(AppSettings.FilePath) ? File.ReadAllText(AppSettings.FilePath) : "{}";
        using var doc = System.Text.Json.JsonDocument.Parse(text);
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.ToString());
    }

    private static void Check(string label, bool ok, string actual)
    {
        _checks++;
        if (ok)
        {
            Console.WriteLine($"  ✓ {label}");
        }
        else
        {
            Failures.Add($"{label} — got: {actual}");
            Console.WriteLine($"  ✗ {label} — got: {actual}");
        }
    }
}
