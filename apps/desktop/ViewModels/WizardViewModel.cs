using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenMod.Desktop.Services;

namespace OpenMod.Desktop.ViewModels;

public enum WizardStep
{
    Welcome = 0,
    Appearance = 1,
    VirtualMic = 2,
    Import = 3,
    Done = 4,
}

/// <summary>
/// One theme card. The mini preview shows *another* palette, so it can't come
/// from the live resources — hence the hardcoded table below. SelfTest asserts
/// that table against Themes/*.axaml so it can't drift.
/// </summary>
public partial class WizardThemeCardViewModel(ThemeMode mode, string label, Action<ThemeMode> select)
    : ViewModelBase
{
    public ThemeMode Mode { get; } = mode;
    public string Label { get; } = label;

    /// <summary>"System" previews the dark palette with a half-moon badge.</summary>
    public bool IsSystem => Mode == ThemeMode.System;

    public IBrush PreviewBg => Palette.PreviewBrush(Mode, "BgBrush");
    public IBrush PreviewPanel => Palette.PreviewBrush(Mode, "PanelBrush");
    public IBrush PreviewCard => Palette.PreviewBrush(Mode, "CardBrush");
    public IBrush PreviewTrack => Palette.PreviewBrush(Mode, "TrackBrush");
    public IBrush PreviewText => Palette.PreviewBrush(Mode, "TextBrush");

    [ObservableProperty]
    private bool _isSelected;

    public IRelayCommand SelectCommand { get; } = new RelayCommand(() => select(mode));
}

public partial class WizardDotViewModel : ViewModelBase
{
    public bool IsCurrent { get; }
    public bool IsComplete { get; }
    public bool IsPending => !IsCurrent;

    public WizardDotViewModel(bool isCurrent, bool isComplete)
    {
        IsCurrent = isCurrent;
        IsComplete = isComplete;
    }
}

/// <summary>
/// One "this is your accent on &lt;theme&gt;" card. The accent has to be resolved
/// per palette here, because the app's accent *text* token is the deep tone on
/// light backgrounds and the bright tone on dark ones — that's the whole point of
/// showing all three at once.
/// </summary>
public sealed class WizardAccentPreviewViewModel
{
    public string Label { get; }
    public ThemeMode Mode { get; }

    public IBrush Surface { get; }
    public IBrush Card { get; }
    public IBrush Text { get; }
    public IBrush Muted { get; }
    public IBrush Track { get; }
    public IBrush AccentText { get; }
    public IBrush AccentSoft { get; }
    public IBrush AccentBorder { get; }
    public IBrush Gradient { get; }
    public IBrush OnAccent { get; }

    public string Hex { get; }

    private WizardAccentPreviewViewModel(ThemeMode mode, string label)
    {
        Mode = mode;
        Label = label;

        var bright = ThemeManager.AccentBright;
        var deep = ThemeManager.AccentDeep;
        var isLight = mode == ThemeMode.Light;

        Surface = Palette.PreviewBrush(mode, "BgBrush");
        Card = Palette.PreviewBrush(mode, "CardBrush");
        Text = Palette.PreviewBrush(mode, "TextBrush");
        Muted = Palette.PreviewBrush(mode, "MutedBrush");
        Track = Palette.PreviewBrush(mode, "TrackBrush");
        OnAccent = Palette.PreviewBrush(mode, "OnAccentBrush");

        // exactly the rule ThemeManager uses, just evaluated for a palette that
        // isn't the active one
        AccentText = new SolidColorBrush(isLight ? deep : bright);
        AccentSoft = new SolidColorBrush(bright, 0.16);
        AccentBorder = new SolidColorBrush(deep, 0.45);
        Gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(bright, 0),
                new GradientStop(deep, 1),
            },
        };

        Hex = ThemeManager.ToHex(bright).ToUpperInvariant();
    }

    public static IReadOnlyList<WizardAccentPreviewViewModel> Build() =>
    [
        new(ThemeMode.Dark, "dark"),
        new(ThemeMode.Light, "light"),
        new(ThemeMode.Amoled, "amoled"),
    ];
}

/// <summary>One "bring your stuff with you" row.</summary>
public partial class ImportRowViewModel : ViewModelBase
{
    public string Name { get; }
    public string Note { get; }
    public string Path { get; }
    public string Count { get; }
    public bool IsAvailable { get; }

    [ObservableProperty]
    private bool _isChecked;

    public IRelayCommand ToggleCommand { get; }

    public ImportRowViewModel(string name, string note, string path, string count, bool available, bool isChecked)
    {
        Name = name;
        Note = note;
        Path = path;
        Count = count;
        IsAvailable = available;
        _isChecked = isChecked && available;
        ToggleCommand = new RelayCommand(() =>
        {
            if (IsAvailable)
            {
                IsChecked = !IsChecked;
            }
        });
    }
}

/// <summary>
/// First-launch wizard: five steps, one idea each. Every appearance choice goes
/// through the same <see cref="ThemeManager"/> and settings file the rest of the
/// app uses, so step 2 is the real thing recolouring itself — there is no
/// preview-only code path to fall out of sync.
/// </summary>
public partial class WizardViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly bool _windowsBranch;

    public ObservableCollection<WizardThemeCardViewModel> Themes { get; } = [];
    public ObservableCollection<AccentSwatchViewModel> Accents { get; } = [];
    public ObservableCollection<WizardDotViewModel> Dots { get; } = [];

    /// <summary>The same accent rendered on dark, light and AMOLED at once.</summary>
    public ObservableCollection<WizardAccentPreviewViewModel> AccentPreviews { get; } = [];
    public ObservableCollection<ImportRowViewModel> ImportRows { get; } = [];

    /// <summary>Raised when the wizard is done (finished or skipped).</summary>
    public event Action<bool>? Finished;

    [ObservableProperty]
    private int _stepIndex;

    [ObservableProperty]
    private string _customHex = "";

    [ObservableProperty]
    private bool _showHexInput;

    [ObservableProperty]
    private string _accentNote = "";

    [ObservableProperty]
    private string _micStatusMessage = "";

    [ObservableProperty]
    private bool _hasMicStatus;

    [ObservableProperty]
    private bool _cableBusy;

    [ObservableProperty]
    private string _cableMessage = "";

    [ObservableProperty]
    private bool _micStatusOk;

    [ObservableProperty]
    private VirtualMicStatus _mic = VirtualMic.Detect();

    [ObservableProperty]
    private string _installerHint = "";

    public WizardViewModel(AppSettings settings, bool? forceWindowsBranch = null)
    {
        _settings = settings;
        _windowsBranch = forceWindowsBranch ?? OperatingSystem.IsWindows();

        BuildThemes();
        BuildAccents();
        BuildImportRows();
        Refresh();

        ThemeManager.Changed += Refresh;
    }

    // ------------------------------------------------------------------ state

    public WizardStep Step => (WizardStep)StepIndex;

    public int StepNumber => StepIndex + 1;

    public bool IsWelcome => StepIndex == (int)WizardStep.Welcome;
    public bool IsAppearance => StepIndex == (int)WizardStep.Appearance;
    public bool IsVirtualMicStep => StepIndex == (int)WizardStep.VirtualMic;
    public bool IsImportStep => StepIndex == (int)WizardStep.Import;
    public bool IsDone => StepIndex == (int)WizardStep.Done;

    /// <summary>Steps 1 and 5 hide the footer action — the hero button *is* the action.</summary>
    public bool ShowFooterAction => StepIndex is > 0 and < 4;

    public bool ShowSkip => ShowFooterAction;

    public string NextLabel
    {
        get
        {
            if (StepIndex == (int)WizardStep.Import)
            {
                var count = ImportRows.Count(r => r.IsChecked);
                return count switch
                {
                    0 => "Continue",
                    1 => "Import 1 item",
                    _ => $"Import {count} items",
                };
            }

            return "Continue";
        }
    }

    public string StepNote => StepIndex switch
    {
        (int)WizardStep.VirtualMic => _windowsBranch ? "step 3 of 5 · windows" : "step 3 of 5 · linux",
        (int)WizardStep.Import => "step 4 of 5",
        _ => "",
    };

    public bool HasStepNote => StepNote.Length > 0;

    public bool IsWindowsBranch => _windowsBranch;

    public bool IsLinuxMic => !_windowsBranch;

    public bool MicReady => Mic.IsReady;

    public string MicName => _windowsBranch ? Mic.CableName ?? Mic.SinkName : Mic.SinkName;

    public string MicMonitor => _windowsBranch ? "CABLE Input" : VirtualMic.MonitorName;

    public string MicDetail => Mic.Detail;

    public string MicLabel => _windowsBranch
        ? (CableDetected ? "detected" : "not detected")
        : Mic.Label;

    public bool CableDetected => Mic.IsReady && Mic.CableName is not null;

    public bool HasCableMessage => !string.IsNullOrWhiteSpace(CableMessage);

    public bool HasAccentNote => !string.IsNullOrWhiteSpace(AccentNote);

    // --------------------------------------------------------------- commands

    [RelayCommand]
    private void Next()
    {
        if (StepIndex >= (int)WizardStep.Done)
        {
            Finish(skipped: false);
            return;
        }

        StepIndex++;
    }

    [RelayCommand]
    private void Back()
    {
        if (StepIndex > 0)
        {
            StepIndex--;
        }
    }

    [RelayCommand]
    private void Skip() => Finish(skipped: true);

    private void Finish(bool skipped)
    {
        Persist();
        _settings.FirstRunComplete = true;
        _settings.Save();
        Finished?.Invoke(skipped);
    }

    [RelayCommand]
    private void OpenApp() => Finish(skipped: false);

    /// <summary>The welcome screen's quiet second option: jump straight to import.</summary>
    [RelayCommand]
    private void GoToImport() => StepIndex = (int)WizardStep.Import;

    [RelayCommand]
    private void ToggleHexInput() => ShowHexInput = !ShowHexInput;

    [RelayCommand]
    private void UseCustomAccent()
    {
        var raw = CustomHex.Trim();
        if (raw.Length == 0)
        {
            AccentNote = "type a hex colour, e.g. #FF99CC";
            return;
        }

        if (!raw.StartsWith('#'))
        {
            raw = "#" + raw;
        }

        if (!Color.TryParse(raw, out var color))
        {
            AccentNote = $"'{CustomHex}' isn't a colour — try #FF99CC";
            return;
        }

        ThemeManager.SetCustomAccent(color);
        AccentNote = $"custom accent {ThemeManager.ToHex(ThemeManager.AccentBright)}";
        Persist();
    }

    [RelayCommand]
    private void UseSystemAccent()
    {
        var ok = ThemeManager.SetMatchSystemAccent(true);
        AccentNote = ok
            ? "following your desktop accent colour"
            : "no settings portal here — pick one of the swatches instead";
        Persist();
    }

    // --- step 3 (linux) -----------------------------------------------------

    [RelayCommand]
    private void RecreateMic()
    {
        var ok = VirtualMic.Ensure(out var detail);
        Mic = VirtualMic.Detect();
        SetMicStatus(detail, ok);
        Refresh();
    }

    [RelayCommand]
    private void TestMic()
    {
        var ok = VirtualMic.PlayTestTone(out var detail);
        SetMicStatus(detail, ok);
    }

    [RelayCommand]
    private async Task CopyMicName()
    {
        var text = _windowsBranch ? MicMonitor : Mic.SinkName;
        var ok = await TryCopyAsync(text);
        SetMicStatus(ok ? $"copied \"{text}\"" : $"copy it by hand: {text}", ok);
    }

    // --- step 3 (windows) ---------------------------------------------------

    [RelayCommand]
    private void OpenVbAudio() => Shell.OpenUrl(CableInstall.PageUrl);

    [RelayCommand]
    private void UseExistingCable()
    {
        Mic = VirtualMic.Detect();
        Refresh();
        CableMessage = Mic.IsReady
            ? $"found {Mic.CableName} — using that as your virtual mic"
            : "couldn't find a virtual cable yet — if you just installed one, reboot first";
    }

    [RelayCommand]
    private async Task InstallCable()
    {
        if (CableBusy)
        {
            return;
        }

        CableBusy = true;
        CableMessage = "starting…";

        try
        {
            var progress = new Progress<string>(message => CableMessage = message);
            var outcome = await CableInstall.InstallAsync(progress);

            CableMessage = outcome.RebootNeeded
                ? outcome.Message + " (a reboot is required)"
                : outcome.Message;

            Mic = VirtualMic.Detect();
            Refresh();
        }
        finally
        {
            CableBusy = false;
        }
    }

    // --- step 4 -------------------------------------------------------------

    [RelayCommand]
    private void ReadReadme() =>
        Shell.OpenUrl("https://github.com/Velinixx/openmod#readme");

    // ------------------------------------------------------------------ build

    private void BuildThemes()
    {
        Themes.Add(new WizardThemeCardViewModel(ThemeMode.System, "System", SelectTheme));
        Themes.Add(new WizardThemeCardViewModel(ThemeMode.Light, "Light", SelectTheme));
        Themes.Add(new WizardThemeCardViewModel(ThemeMode.Dark, "Dark", SelectTheme));
        Themes.Add(new WizardThemeCardViewModel(ThemeMode.Amoled, "AMOLED", SelectTheme));
    }

    private void BuildAccents()
    {
        foreach (var preset in ThemeManager.Accents)
        {
            Accents.Add(new AccentSwatchViewModel(preset.Name, preset.Bright, preset.Deep, SelectAccent));
        }
    }

    private void BuildImportRows()
    {
        var presets = PresetLibrary.Scan().Count;
        ImportRows.Add(new ImportRowViewModel(
            "Presets",
            "pitch, formant and gain — your chains come along",
            "effects/*.json",
            presets == 1 ? "1 found" : $"{presets} found",
            available: presets > 0,
            isChecked: presets > 0));

        var clips = SoundboardLibrary.Count();
        ImportRows.Add(new ImportRowViewModel(
            "Soundboard",
            "drop .wav .mp3 .ogg .flac files in any time",
            SoundboardLibrary.DisplayPath,
            clips == 1 ? "1 clip" : $"{clips} clips",
            available: true,
            isChecked: true));

        var voicemod = SoundboardLibrary.FindVoicemod();
        ImportRows.Add(new ImportRowViewModel(
            "Voicemod presets",
            "we read their JSON and map what we can — best effort",
            voicemod.Path,
            voicemod.Found ? "found" : "not on this machine",
            available: voicemod.Found,
            isChecked: voicemod.Found));

        // ticking a row changes "Import 2 items" -> "Import 3 items"
        foreach (var row in ImportRows)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ImportRowViewModel.IsChecked))
                {
                    OnPropertyChanged(nameof(NextLabel));
                }
            };
        }
    }

    // ---------------------------------------------------------------- actions

    private void SelectTheme(ThemeMode mode)
    {
        ThemeManager.SetMode(mode);
        Persist();
    }

    private void SelectAccent(string name)
    {
        ThemeManager.SetAccentPreset(name);
        AccentNote = "";
        Persist();
    }

    private void Persist()
    {
        ThemeManager.WriteTo(_settings);

        // only remember the soundboard folder if they actually ticked it
        var soundboard = ImportRows.FirstOrDefault(r => r.Name == "Soundboard");
        _settings.SoundboardDir = soundboard is { IsChecked: true } ? SoundboardLibrary.Dir : null;
        _settings.Save();
    }

    private void Refresh()
    {
        foreach (var theme in Themes)
        {
            theme.IsSelected = theme.Mode == ThemeManager.Mode;
        }

        foreach (var accent in Accents)
        {
            accent.IsSelected = accent.Name.Equals(ThemeManager.AccentName, StringComparison.OrdinalIgnoreCase);
        }

        RebuildDots();
        RebuildAccentPreviews();

        OnPropertyChanged(nameof(Step));
        OnPropertyChanged(nameof(StepNumber));
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsAppearance));
        OnPropertyChanged(nameof(IsVirtualMicStep));
        OnPropertyChanged(nameof(IsImportStep));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(ShowFooterAction));
        OnPropertyChanged(nameof(ShowSkip));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(StepNote));
        OnPropertyChanged(nameof(HasStepNote));
        OnPropertyChanged(nameof(MicReady));
        OnPropertyChanged(nameof(MicName));
        OnPropertyChanged(nameof(MicMonitor));
        OnPropertyChanged(nameof(MicDetail));
        OnPropertyChanged(nameof(MicLabel));
        OnPropertyChanged(nameof(CableDetected));
        OnPropertyChanged(nameof(HasCableMessage));
        OnPropertyChanged(nameof(HasAccentNote));
    }

    private void RebuildAccentPreviews()
    {
        AccentPreviews.Clear();
        foreach (var preview in WizardAccentPreviewViewModel.Build())
        {
            AccentPreviews.Add(preview);
        }
    }

    private void RebuildDots()
    {
        Dots.Clear();
        for (var i = 0; i < 5; i++)
        {
            Dots.Add(new WizardDotViewModel(isCurrent: i == StepIndex, isComplete: i < StepIndex));
        }
    }

    private void SetMicStatus(string message, bool ok)
    {
        MicStatusMessage = message;
        HasMicStatus = !string.IsNullOrWhiteSpace(message);
        MicStatusOk = ok;
    }

    private static async Task<bool> TryCopyAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return false;
            }

            var window = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.Windows.FirstOrDefault();
            if (window?.Clipboard is not { } clipboard)
            {
                return false;
            }

            // Avalonia 12 replaced SetTextAsync with the data-transfer model
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(text));
            await clipboard.SetDataAsync(data);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    partial void OnStepIndexChanged(int value)
    {
        RebuildDots();
        OnPropertyChanged(nameof(Step));
        OnPropertyChanged(nameof(StepNumber));
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsAppearance));
        OnPropertyChanged(nameof(IsVirtualMicStep));
        OnPropertyChanged(nameof(IsImportStep));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(ShowFooterAction));
        OnPropertyChanged(nameof(ShowSkip));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(StepNote));
        OnPropertyChanged(nameof(HasStepNote));
    }

    partial void OnMicChanged(VirtualMicStatus value) => Refresh();

    partial void OnCableMessageChanged(string value) => OnPropertyChanged(nameof(HasCableMessage));

    partial void OnAccentNoteChanged(string value) => OnPropertyChanged(nameof(HasAccentNote));
}

/// <summary>
/// Preview colours for the four theme cards. These are pictures of *other*
/// palettes, so they can't be DynamicResource lookups — SelfTest checks this
/// table against the real Themes/*.axaml dictionaries.
/// </summary>
public static class Palette
{
    private static readonly Dictionary<ThemeMode, Dictionary<string, string>> Preview = new()
    {
        [ThemeMode.System] = new()
        {
            ["BgBrush"] = "#23272F", ["PanelBrush"] = "#1B1F26", ["CardBrush"] = "#2C323C",
            ["TrackBrush"] = "#3A4250", ["TextBrush"] = "#ECEFF4", ["SoftBrush"] = "#AEB8C6",
            ["MutedBrush"] = "#6E7A8A", ["OnAccentBrush"] = "#2A0E1A",
        },
        [ThemeMode.Dark] = new()
        {
            ["BgBrush"] = "#23272F", ["PanelBrush"] = "#1B1F26", ["CardBrush"] = "#2C323C",
            ["TrackBrush"] = "#3A4250", ["TextBrush"] = "#ECEFF4", ["SoftBrush"] = "#AEB8C6",
            ["MutedBrush"] = "#6E7A8A", ["OnAccentBrush"] = "#2A0E1A",
        },
        [ThemeMode.Light] = new()
        {
            ["BgBrush"] = "#F2F5F9", ["PanelBrush"] = "#FFFFFF", ["CardBrush"] = "#FFFFFF",
            ["TrackBrush"] = "#DCE3EC", ["TextBrush"] = "#161B22", ["SoftBrush"] = "#4A5563",
            ["MutedBrush"] = "#7B8794", ["OnAccentBrush"] = "#2A0E1A",
        },
        [ThemeMode.Amoled] = new()
        {
            ["BgBrush"] = "#000000", ["PanelBrush"] = "#000000", ["CardBrush"] = "#0C0C0C",
            ["TrackBrush"] = "#262626", ["TextBrush"] = "#FFFFFF", ["SoftBrush"] = "#B4B4B4",
            ["MutedBrush"] = "#6E6E6E", ["OnAccentBrush"] = "#12060C",
        },
    };

    public static IBrush PreviewBrush(ThemeMode mode, string key) =>
        Preview.TryGetValue(mode, out var palette) && palette.TryGetValue(key, out var hex)
            ? new SolidColorBrush(Color.Parse(hex))
            : Brushes.Transparent;

    public static string PreviewHex(ThemeMode mode, string key) =>
        Preview.TryGetValue(mode, out var palette) && palette.TryGetValue(key, out var hex) ? hex : "";
}
