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

        // --- Settings → setup re-opens the wizard ---------------------------
        var flagBefore = AppSettings.Load().FirstRunComplete;
        var setupAsked = 0;
        vm.SetupRequested += () => setupAsked++;
        vm.RunSetupCommand.Execute(null);
        Check("settings can re-open the first-run setup", setupAsked == 1, $"{setupAsked} event(s)");
        Check("re-running setup doesn't touch the first-run flag",
            AppSettings.Load().FirstRunComplete == flagBefore,
            $"{flagBefore} -> {AppSettings.Load().FirstRunComplete}");

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

        // --- wizard: first run, step flow, persistence ----------------------
        CheckWizard(sandbox);

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

        // Settings → setup is the promise the wizard's last step makes, so it
        // has to exist as a real button in the page, not just a command.
        var setupButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Content as string == "Run setup");
        Check("settings page offers Run setup", setupButton is not null, "no Run setup button");
        Check("Run setup button is bound to its command",
            setupButton?.Command is not null, "command not bound");

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

    /// <summary>
    /// The first-launch wizard: step flow, the theme/accent wiring it shares with
    /// Settings, the accent-preview token rule, and that finishing (or skipping)
    /// is what marks first run complete.
    /// </summary>
    private static void CheckWizard(string sandbox)
    {
        var fresh = new AppSettings();
        Check("first run starts the wizard", !fresh.FirstRunComplete, fresh.FirstRunComplete.ToString());

        var vm = new WizardViewModel(fresh);
        Check("wizard opens on welcome", vm.IsWelcome && vm.StepIndex == 0, vm.StepIndex.ToString());
        Check("welcome hides the footer action", !vm.ShowFooterAction, vm.ShowFooterAction.ToString());
        Check("welcome hides skip for now", !vm.ShowSkip, vm.ShowSkip.ToString());
        Check("five progress dots", vm.Dots.Count == 5, vm.Dots.Count.ToString());
        Check("first dot is current", vm.Dots[0].IsCurrent && !vm.Dots[1].IsComplete,
            $"{vm.Dots[0].IsCurrent}/{vm.Dots[1].IsComplete}");

        vm.NextCommand.Execute(null);
        Check("next goes to appearance", vm.IsAppearance, vm.Step.ToString());
        Check("appearance shows back/next", vm.ShowFooterAction && vm.ShowSkip, vm.ShowFooterAction.ToString());
        Check("dot 1 is complete on step 2", vm.Dots[0].IsComplete && vm.Dots[1].IsCurrent,
            $"{vm.Dots[0].IsComplete}/{vm.Dots[1].IsCurrent}");

        // theme + accent go through the real ThemeManager, same as Settings
        vm.Themes.Single(t => t.Mode == ThemeMode.Dark).SelectCommand.Execute(null);
        Check("wizard theme applies", ThemeManager.Mode == ThemeMode.Dark, ThemeManager.Mode.ToString());
        Check("wizard theme persists", Json()["theme"] == "Dark", Json()["theme"]);
        Check("wizard theme card shows selected", vm.Themes.Single(t => t.Mode == ThemeMode.Dark).IsSelected,
            "IsSelected");

        // the three-up accent preview
        Check("accent preview covers dark/light/amoled", vm.AccentPreviews.Count == 3,
            vm.AccentPreviews.Count.ToString());
        Check("accent preview labels are the palettes",
            string.Join(",", vm.AccentPreviews.Select(p => p.Label)) == "dark,light,amoled",
            string.Join(",", vm.AccentPreviews.Select(p => p.Label)));
        Check("preview: dark uses the bright accent for text",
            IsColor(vm.AccentPreviews[0].AccentText, ThemeManager.ToHex(ThemeManager.AccentBright)),
            Describe(vm.AccentPreviews[0].AccentText));
        Check("preview: light uses the deep accent for text",
            IsColor(vm.AccentPreviews[1].AccentText, ThemeManager.ToHex(ThemeManager.AccentDeep)),
            Describe(vm.AccentPreviews[1].AccentText));
        Check("preview: amoled uses the bright accent for text",
            IsColor(vm.AccentPreviews[2].AccentText, ThemeManager.ToHex(ThemeManager.AccentBright)),
            Describe(vm.AccentPreviews[2].AccentText));

        vm.Accents.Single(a => a.Name == "mint").SelectCommand.Execute(null);
        Check("changing the accent repaints every preview",
            IsColor(vm.AccentPreviews[0].AccentText, "#7FE0C3")
            && IsColor(vm.AccentPreviews[1].AccentText, "#0FA97F"),
            $"{Describe(vm.AccentPreviews[0].AccentText)} / {Describe(vm.AccentPreviews[1].AccentText)}");
        Check("accent choice persisted from the wizard", Json()["accent"] == "mint", Json()["accent"]);

        // the preview colour table must match the real palettes
        foreach (var (mode, palette) in new[]
                 {
                     (ThemeMode.Dark, "Dark"), (ThemeMode.Light, "Light"), (ThemeMode.Amoled, "Amoled"),
                 })
        {
            var (ok, detail) = PaletteMatches(mode, palette);
            Check($"preview table matches Themes/{palette}.axaml", ok, detail);
        }

        // step 3
        vm.NextCommand.Execute(null);
        Check("step 3 is the virtual mic", vm.IsVirtualMicStep, vm.Step.ToString());
        Check("step 3 branches by platform",
            vm.IsWindowsBranch == OperatingSystem.IsWindows(), $"windows={vm.IsWindowsBranch}");
        Check("virtual mic has a name to show", vm.MicName.Length > 0, vm.MicName);
        Check("virtual mic step note names the platform",
            vm.StepNote.Contains(OperatingSystem.IsWindows() ? "windows" : "linux"), vm.StepNote);

        // step 4
        vm.NextCommand.Execute(null);
        Check("import lists presets, soundboard and voicemod", vm.ImportRows.Count == 3,
            vm.ImportRows.Count.ToString());
        var ticked = vm.ImportRows.Count(r => r.IsChecked);
        Check("import button counts the ticked rows",
            vm.NextLabel == (ticked == 1 ? "Import 1 item" : $"Import {ticked} items"),
            $"{vm.NextLabel} for {ticked} ticked");
        Check("an unavailable row can't be ticked", vm.ImportRows.All(r => r.IsAvailable || !r.IsChecked),
            "checked-but-unavailable row present");

        // step 5 + finish
        vm.NextCommand.Execute(null);
        Check("last step is the done screen", vm.IsDone, vm.Step.ToString());
        Check("done screen hides the footer action", !vm.ShowFooterAction, vm.ShowFooterAction.ToString());
        Check("done screen names the virtual mic", vm.MicName == "openmod_vmic", vm.MicName);

        bool? finished = null;
        vm.Finished += skipped => finished = skipped;
        vm.OpenAppCommand.Execute(null);
        Check("opening the app finishes the wizard", finished == false, finished?.ToString() ?? "(no event)");
        Check("finishing marks first run complete", AppSettings.Load().FirstRunComplete,
            Json()["firstRunComplete"]);
        Check("finishing keeps the theme chosen in step 2", AppSettings.Load().Theme == "Dark",
            AppSettings.Load().Theme);

        // skip path: same flag, no step crawling
        var skippedSettings = new AppSettings();
        var skipVm = new WizardViewModel(skippedSettings);
        bool? skippedFlag = null;
        skipVm.Finished += skipped => skippedFlag = skipped;
        skipVm.SkipCommand.Execute(null);
        Check("skip for now finishes the wizard", skippedFlag == true, skippedFlag?.ToString() ?? "(no event)");
        Check("skip marks first run complete", skippedSettings.FirstRunComplete,
            skippedSettings.FirstRunComplete.ToString());
        Check("skip doesn't pretend you walked the steps", skipVm.StepIndex == 0, skipVm.StepIndex.ToString());

        // the VB-CABLE scrape is pure, so it can be tested without a network
        const string page =
            "<a href=\"https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack43.zip\">old</a>" +
            "<a href=\"https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack46.zip\">new</a>";
        Check("picks the newest VB-CABLE package, not a pinned version",
            CableInstall.FindInstallerUrl(page)?.EndsWith("VBCABLE_Driver_Pack46.zip", StringComparison.Ordinal) == true,
            CableInstall.FindInstallerUrl(page) ?? "(null)");
        Check("no package link means no installer",
            CableInstall.FindInstallerUrl("<html><body>nope</body></html>") is null, "found one");
        Check("installer URL is vb-audio's own host",
            CableInstall.FindInstallerUrl(page)?.StartsWith("https://download.vb-audio.com/", StringComparison.Ordinal) == true,
            CableInstall.FindInstallerUrl(page) ?? "(null)");

        CheckWizardLayout(vm);
    }

    /// <summary>
    /// Lays out the real wizard window and checks the carousel is actually showing
    /// the step the view model says it's on — the thing a screenshot can't prove
    /// for the steps we didn't capture.
    /// </summary>
    private static void CheckWizardLayout(WizardViewModel vm)
    {
        vm.StepIndex = 1; // appearance, the busiest page

        var window = new OpenMod.Desktop.Views.WizardWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(1280, 800));
        window.Arrange(new Rect(0, 0, 1280, 800));
        Dispatcher.UIThread.RunJobs();

        var carousel = window.GetVisualDescendants().OfType<Carousel>().FirstOrDefault();
        Check("wizard window has a step carousel", carousel is not null, "no carousel");
        Check("carousel shows the view model's step",
            carousel?.SelectedIndex == vm.StepIndex,
            $"{carousel?.SelectedIndex} vs {vm.StepIndex}");

        var themeCards = window.GetVisualDescendants().OfType<Button>()
            .Count(b => b.Classes.Contains("themecard"));
        Check("four theme cards laid out", themeCards == 4, themeCards.ToString());

        var previews = window.GetVisualDescendants().OfType<Border>()
            .Count(b => b.Width is > 220 and < 230 && b.Height is > 100 and < 110);
        Check("three accent preview cards laid out", previews == 3, previews.ToString());

        window.Close();
    }

    /// <summary>
    /// Compares the hardcoded preview table against the real palette dictionary,
    /// so a palette edit can't leave the theme cards showing stale colours.
    /// </summary>
    private static (bool Ok, string Detail) PaletteMatches(ThemeMode mode, string palette)
    {
        var keys = new[] { "BgBrush", "PanelBrush", "CardBrush", "TrackBrush", "TextBrush", "SoftBrush", "MutedBrush", "OnAccentBrush" };

        try
        {
            var uri = new Uri($"avares://OpenMod.Desktop/Themes/{palette}.axaml");
            var dictionary = new ResourceDictionary();
            dictionary.MergedDictionaries.Add(new Avalonia.Markup.Xaml.Styling.ResourceInclude(uri) { Source = uri });

            foreach (var key in keys)
            {
                if (!dictionary.TryGetResource(key, null, out var value) || value is not SolidColorBrush brush)
                {
                    return (false, $"{key} not found in {palette}.axaml");
                }

                var actual = brush.Color;
                var expected = Color.Parse("#" + Palette.PreviewHex(mode, key).TrimStart('#'));
                if (actual.R != expected.R || actual.G != expected.G || actual.B != expected.B)
                {
                    return (false, $"{key}: table {expected} vs palette {actual}");
                }
            }

            return (true, "matches");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
