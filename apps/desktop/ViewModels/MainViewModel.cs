using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenMod.Desktop.Models;
using OpenMod.Desktop.Services;

namespace OpenMod.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const int SampleRate = 44100;

    public ObservableCollection<NavItemViewModel> Nav { get; } = [];
    public ObservableCollection<EffectRegistryItemViewModel> EffectRegistry { get; } = [];
    public ObservableCollection<FolderItemViewModel> Folders { get; } = [];

    /// <summary>Voice cards + the trailing "new voice" tile (two template types).</summary>
    public ObservableCollection<ViewModelBase> Voices { get; } = [];

    public ObservableCollection<CommunityPresetViewModel> Community { get; } = [];
    public ObservableCollection<ChainEffectViewModel> Chain { get; } = [];
    public ObservableCollection<StatRowViewModel> Stats { get; } = [];

    [ObservableProperty]
    private bool _isVoiceOn = true;

    [ObservableProperty]
    private AppPage _page = AppPage.Voices;

    /// <summary>True once every preset JSON has been parsed (lazy load finished).</summary>
    [ObservableProperty]
    private bool _isHydrated;

    [ObservableProperty]
    private string _activeChainName = "—";

    [ObservableProperty]
    private string _voicesSubtitle = "scanning effects/…";

    [ObservableProperty]
    private string _soundboardSubtitle = "scanning soundboards/…";

    [ObservableProperty]
    private string _latencyText = "—";

    [ObservableProperty]
    private string _inputDevice = "default · 2ch 44.1kHz";

    [ObservableProperty]
    private string _outputDevice = "openmod_vmic";

    [ObservableProperty]
    private string _presetPath = "";

    // meter / slider geometry (track width 120, chain track 232) — pre-computed
    // so the XAML needs no converters
    [ObservableProperty]
    private double _inMeterWidth = 74.4;

    [ObservableProperty]
    private double _outMeterWidth = 57.6;

    [ObservableProperty]
    private double _volumeWidth = 90;

    [ObservableProperty]
    private double _volumeKnobX = 84;

    public string VoiceToggleText => IsVoiceOn ? "VOICE ON" : "VOICE OFF";

    /// <summary>Mirrors <see cref="SettingsViewModel.Monitor"/> so the bottom bar and
    /// the settings page can never disagree.</summary>
    public bool IsMonitoring => Settings.Monitor;

    public string MonitorText => IsMonitoring ? "on" : "off";

    public SettingsViewModel Settings { get; }

    public string SoundboardPath { get; } = Path.Combine(PresetLibrary.RepoRoot, "soundboards");

    public MainViewModel() : this(AppSettings.Load())
    {
    }

    public MainViewModel(AppSettings settings)
    {
        Settings = new SettingsViewModel(settings);
        Settings.MonitorChanged += _ =>
        {
            OnPropertyChanged(nameof(IsMonitoring));
            OnPropertyChanged(nameof(MonitorText));
        };

        BuildNav();
        BuildRegistry();
        ScanVoices();      // cheap: file names only
        BuildCommunity();
        BuildStats();
        BuildFolders();
        _ = HydrateAsync(); // expensive work happens after the window is up
    }

    // ---------------------------------------------------------------- startup

    private void BuildNav()
    {
        // icon paths are 16x16, stroked
        Nav.Add(new NavItemViewModel("Voices", "M2,2 H7 V7 H2 Z M9,2 H14 V7 H9 Z M2,9 H7 V14 H2 Z M9,9 H14 V14 H9 Z",
            AppPage.Voices, () => ShowPage(AppPage.Voices)) { IsActive = true });
        Nav.Add(new NavItemViewModel("Soundboard", "M1.5,3.5 H14.5 M1.5,8 H9.5 M1.5,12.5 H12.5",
            AppPage.Soundboard, () => ShowPage(AppPage.Soundboard)));
        Nav.Add(new NavItemViewModel("Settings", "M1.5,4 H14.5 M1.5,12 H14.5 M5.5,1.5 V6.5 M11,9.5 V14.5",
            AppPage.Settings, () => ShowPage(AppPage.Settings)));
    }

    private void BuildRegistry()
    {
        foreach (var name in new[] { "pitch_shift", "gain", "formant", "reverb", "distortion" })
        {
            EffectRegistry.Add(new EffectRegistryItemViewModel(name));
        }
    }

    /// <summary>Metadata-only pass: one directory listing, no JSON parsing.</summary>
    private void ScanVoices()
    {
        var paths = PresetLibrary.Scan();

        foreach (var path in paths)
        {
            Voices.Add(new VoiceCardViewModel(path));
        }

        Voices.Add(new NewVoiceCardViewModel());

        if (paths.Count > 0)
        {
            ((VoiceCardViewModel)Voices[0]).IsActive = true;
            VoicesSubtitle = $"{paths.Count} presets · JSON files in effects/";
            SoundboardSubtitle = "clips in soundboards/ · names only until played";
        }
        else
        {
            VoicesSubtitle = "no presets yet · drop a JSON file into effects/";
            SoundboardSubtitle = "drop audio files in soundboards/";
        }
    }

    private void BuildCommunity()
    {
        Community.Add(new CommunityPresetViewModel("anime-girl", "@mika", "pitch +9.0 · formant +2.0"));
        Community.Add(new CommunityPresetViewModel("radio-static", "@kuro", "distortion 0.4 · bandpass 300–3k"));
        Community.Add(new CommunityPresetViewModel("demon-lord", "@vel", "pitch -8.0 · reverb 0.6 · gain +2"));
    }

    private void BuildStats()
    {
        Stats.Add(new StatRowViewModel("algorithmic latency", "—"));
        Stats.Add(new StatRowViewModel("queued", "0 ms"));
        Stats.Add(new StatRowViewModel("underruns", "0"));
        Stats.Add(new StatRowViewModel("overruns", "0"));
    }

    private void BuildFolders()
    {
        var presets = Voices.OfType<VoiceCardViewModel>().Count();
        Folders.Add(new FolderItemViewModel("effects/", $"{presets} files"));
        Folders.Add(new FolderItemViewModel("soundboards/", "0 clips"));
        Folders.Add(new FolderItemViewModel("themes/", "soon"));
    }

    // ----------------------------------------------------------- lazy loading

    /// <summary>
    /// Parses each preset JSON on a worker thread, then applies it to the card.
    /// The window is already interactive while this runs.
    /// </summary>
    public async Task HydrateAsync()
    {
        var cards = Voices.OfType<VoiceCardViewModel>().ToList();

        foreach (var card in cards)
        {
            var path = card.Path;
            var preset = await Task.Run(() => PresetLibrary.Load(path));
            card.Hydrate(preset, SampleRate);
        }

        var active = cards.FirstOrDefault(c => c.IsActive) ?? cards.FirstOrDefault();
        if (active is not null)
        {
            ApplyChain(active);
        }

        IsHydrated = true;
    }

    private void ApplyChain(VoiceCardViewModel card)
    {
        var preset = PresetLibrary.Load(card.Path);

        Chain.Clear();
        foreach (var effect in preset.Effects)
        {
            Chain.Add(BuildChainCard(effect));
        }

        foreach (var item in EffectRegistry)
        {
            item.IsUsed = preset.Effects.Any(e => e.Type == item.Name);
        }

        var ms = PresetLibrary.LatencyMs(preset, SampleRate);
        ActiveChainName = card.Name;
        PresetPath = $"effects/{card.FileName}";
        LatencyText = $"{ms:0.0} ms";
        Stats[0].Value = $"{ms:0.0} ms";
    }

    private static ChainEffectViewModel BuildChainCard(PresetEffect effect)
    {
        switch (effect.Type)
        {
            case "pitch_shift":
            {
                var semitones = effect.Params.GetValueOrDefault("semitones");
                return new ChainEffectViewModel(
                    "pitch_shift", "semitones", PresetLibrary.Format(semitones),
                    (semitones + 12) / 24);
            }
            case "gain":
            {
                var db = effect.Params.GetValueOrDefault("gain_db");
                return new ChainEffectViewModel(
                    "gain", "gain_db", PresetLibrary.Format(db),
                    (db + 24) / 36);
            }
            default:
                return new ChainEffectViewModel(effect.Type, "amount", "—", 0.5);
        }
    }

    // ------------------------------------------------------------------- pages

    public bool IsVoicesPage => Page == AppPage.Voices;
    public bool IsSoundboardPage => Page == AppPage.Soundboard;
    public bool IsSettingsPage => Page == AppPage.Settings;

    /// <summary>The chain panel belongs to the voices/soundboard pages, not settings.</summary>
    public bool ShowChainPanel => Page != AppPage.Settings;

    public void ShowPage(AppPage page) => Page = page;

    partial void OnPageChanged(AppPage value)
    {
        foreach (var item in Nav)
        {
            item.IsActive = item.Page == value;
        }

        OnPropertyChanged(nameof(IsVoicesPage));
        OnPropertyChanged(nameof(IsSoundboardPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(ShowChainPanel));
    }

    // --------------------------------------------------------------- commands

    [RelayCommand]
    private void ToggleVoice() => IsVoiceOn = !IsVoiceOn;

    [RelayCommand]
    private void ToggleMonitor() => Settings.Monitor = !Settings.Monitor;

    [RelayCommand]
    private void GoToSettings() => ShowPage(AppPage.Settings);

    [RelayCommand]
    private static void Stop()
    {
        // audio engine not wired to the UI yet — the Rust core owns the stream
    }

    partial void OnIsVoiceOnChanged(bool value) => OnPropertyChanged(nameof(VoiceToggleText));
}
