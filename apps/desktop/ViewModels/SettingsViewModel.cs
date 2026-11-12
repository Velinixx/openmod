using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenMod.Desktop.Services;

namespace OpenMod.Desktop.ViewModels;

public partial class ThemeOptionViewModel(ThemeMode mode, string label, Action<ThemeMode> select) : ViewModelBase
{
    public ThemeMode Mode { get; } = mode;
    public string Label { get; } = label;

    [ObservableProperty]
    private bool _isSelected;

    public IRelayCommand SelectCommand { get; } = new RelayCommand(() => select(mode));
}

public partial class AccentSwatchViewModel : ViewModelBase
{
    public string Name { get; }
    public Color Bright { get; }
    public Color Deep { get; }
    public IBrush Swatch { get; }
    public IBrush DeepBrush { get; }

    [ObservableProperty]
    private bool _isSelected;

    public IRelayCommand SelectCommand { get; }

    public AccentSwatchViewModel(string name, Color bright, Color deep, Action<string> select)
    {
        Name = name;
        Bright = bright;
        Deep = deep;
        Swatch = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(bright, 0),
                new GradientStop(deep, 1),
            },
        };
        DeepBrush = new SolidColorBrush(deep);
        SelectCommand = new RelayCommand(() => select(name));
    }
}

/// <summary>
/// Appearance + audio settings. Every control writes straight through to
/// <see cref="ThemeManager"/> and then persists to
/// <c>~/.config/openmod/settings.json</c> — no Apply button to forget.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private bool _suspend;

    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = [];
    public ObservableCollection<AccentSwatchViewModel> Accents { get; } = [];

    [ObservableProperty]
    private bool _matchSystemAccent;

    [ObservableProperty]
    private bool _monitor;

    [ObservableProperty]
    private string _customHex = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _hasStatus;

    [ObservableProperty]
    private string _inputDevice = "default · 2ch 44.1kHz";

    [ObservableProperty]
    private string _outputDevice = "openmod_vmic";

    [ObservableProperty]
    private string _accentNameLabel = "blush";

    [ObservableProperty]
    private string _accentBrightHex = "";

    [ObservableProperty]
    private string _accentDeepHex = "";

    [ObservableProperty]
    private string _onAccentHex = "#2A0E1A";

    [ObservableProperty]
    private string _previewCaption = "";

    [ObservableProperty]
    private IBrush _accentSwatchBrush = Brushes.Transparent;

    [ObservableProperty]
    private IBrush _accentDeepSwatchBrush = Brushes.Transparent;

    [ObservableProperty]
    private IBrush _onAccentSwatchBrush = Brushes.Transparent;

    [ObservableProperty]
    private string _settingsPath = AppSettings.FilePath;

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        _monitor = settings.Monitor;
        _inputDevice = settings.InputDevice ?? "default · 2ch 44.1kHz";

        BuildThemes();
        BuildAccents();

        ThemeManager.Changed += Refresh;
        Refresh();
    }

    // ------------------------------------------------------------------ build

    private void BuildThemes()
    {
        Themes.Add(new ThemeOptionViewModel(ThemeMode.System, "System", SelectTheme));
        Themes.Add(new ThemeOptionViewModel(ThemeMode.Light, "Light", SelectTheme));
        Themes.Add(new ThemeOptionViewModel(ThemeMode.Dark, "Dark", SelectTheme));
        Themes.Add(new ThemeOptionViewModel(ThemeMode.Amoled, "AMOLED", SelectTheme));
    }

    private void BuildAccents()
    {
        foreach (var preset in ThemeManager.Accents)
        {
            Accents.Add(new AccentSwatchViewModel(preset.Name, preset.Bright, preset.Deep, SelectAccent));
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
        MatchSystemAccent = false;
        _suspend = true;
        _settings.MatchSystemAccent = false;
        _suspend = false;
        Persist();
    }

    [RelayCommand]
    private void ToggleMatchSystemAccent()
    {
        if (_suspend)
        {
            return;
        }

        var wanted = !ThemeManager.MatchSystemAccent;
        var ok = ThemeManager.SetMatchSystemAccent(wanted);

        _suspend = true;
        MatchSystemAccent = ThemeManager.MatchSystemAccent;
        _suspend = false;

        SetStatus(ok
            ? wanted
                ? "following your desktop accent colour"
                : "using your own accent colour"
            : "couldn't read a system accent (no settings portal) — keeping yours");

        Persist();
    }

    [RelayCommand]
    private void UseCustomAccent()
    {
        var raw = CustomHex.Trim();
        if (raw.Length == 0)
        {
            SetStatus("type a hex colour, e.g. #FF99CC");
            return;
        }

        if (!raw.StartsWith('#'))
        {
            raw = "#" + raw;
        }

        if (!Color.TryParse(raw, out var color))
        {
            SetStatus($"'{CustomHex}' isn't a colour — try #FF99CC");
            return;
        }

        ThemeManager.SetCustomAccent(color);
        SetStatus($"custom accent {ThemeManager.ToHex(ThemeManager.AccentBright)}");
        Persist();
    }

    [RelayCommand]
    private void ToggleMonitor() => Monitor = !Monitor;

    // ----------------------------------------------------------------- state

    private void Refresh()
    {
        _suspend = true;

        foreach (var theme in Themes)
        {
            theme.IsSelected = theme.Mode == ThemeManager.Mode;
        }

        var accentName = ThemeManager.AccentName;
        foreach (var accent in Accents)
        {
            accent.IsSelected = accent.Name.Equals(accentName, StringComparison.OrdinalIgnoreCase);
        }

        MatchSystemAccent = ThemeManager.MatchSystemAccent;
        AccentNameLabel = accentName;
        AccentBrightHex = ThemeManager.ToHex(ThemeManager.AccentBright).ToUpperInvariant();
        AccentDeepHex = ThemeManager.ToHex(ThemeManager.AccentDeep).ToUpperInvariant();
        OnAccentHex = ThemeManager.Mode == ThemeMode.Amoled ? "#12060C" : "#2A0E1A";
        PreviewCaption = $"{ThemeManager.Mode.ToString().ToLowerInvariant()} · {accentName}";

        AccentSwatchBrush = new SolidColorBrush(ThemeManager.AccentBright);
        AccentDeepSwatchBrush = new SolidColorBrush(ThemeManager.AccentDeep);
        OnAccentSwatchBrush = new SolidColorBrush(Color.Parse(OnAccentHex));

        _suspend = false;
    }

    private void SetStatus(string message)
    {
        StatusMessage = message;
        HasStatus = !string.IsNullOrWhiteSpace(message);
    }

    private void Persist()
    {
        ThemeManager.WriteTo(_settings);
        _settings.Monitor = Monitor;
        _settings.Save();
    }

    partial void OnMonitorChanged(bool value)
    {
        if (_suspend)
        {
            return;
        }

        _settings.Monitor = value;
        _settings.Save();
        MonitorChanged?.Invoke(value);
    }

    /// <summary>
    /// Re-opens the first-launch wizard. The wizard's own footnote promises
    /// "this lives in Settings → setup", so this is that promise kept.
    /// </summary>
    [RelayCommand]
    private void RunSetup() => SetupRequested?.Invoke();

    /// <summary>Lets the shell open the wizard window.</summary>
    public event Action? SetupRequested;

    /// <summary>Lets the shell mirror the monitor toggle in the bottom bar.</summary>
    public event Action<bool>? MonitorChanged;
}
