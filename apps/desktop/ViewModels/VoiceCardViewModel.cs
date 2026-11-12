using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenMod.Desktop.Models;
using OpenMod.Desktop.Services;

namespace OpenMod.Desktop.ViewModels;

/// <summary>
/// One voice card. Constructed from the file *name* only — the JSON body is
/// parsed later by <see cref="Hydrate"/>, so startup never touches disk content.
/// </summary>
public partial class VoiceCardViewModel : ViewModelBase
{
    public string Path { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _latencyText = "—";

    [ObservableProperty]
    private bool _isActive;

    /// <summary>False until the preset JSON has been parsed.</summary>
    [ObservableProperty]
    private bool _isHydrated;

    public ObservableCollection<string> Chips { get; } = [];

    public VoiceCardViewModel(string path)
    {
        Path = path;
        _name = System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Parse-on-demand: called once, lazily, from a background task.</summary>
    public void Hydrate(PresetData preset, int sampleRate)
    {
        if (!string.IsNullOrWhiteSpace(preset.Name))
        {
            Name = preset.Name!;
        }

        Description = preset.Description ?? "";
        Chips.Clear();
        foreach (var effect in preset.Effects)
        {
            Chips.Add(PresetLibrary.Chip(effect));
        }

        if (Chips.Count == 0)
        {
            Chips.Add("no effects");
        }

        var ms = PresetLibrary.LatencyMs(preset, sampleRate);
        LatencyText = $"{ms:0.0} ms";
        IsHydrated = true;
    }
}

public enum AppPage
{
    Voices,
    Soundboard,
    Settings,
}

/// <summary>The dashed "new voice" tile that lives at the end of the grid.</summary>
public sealed class NewVoiceCardViewModel : ViewModelBase;

/// <summary>Placeholder rows for community presets (GitHub-backed later).</summary>
public sealed class CommunityPresetViewModel(string name, string author, string meta) : ViewModelBase
{
    public string Name { get; } = name;
    public string Author { get; } = author;
    public string Meta { get; } = meta;
    public string Detail => $"{Meta}  —  {Author}";
}

public sealed partial class NavItemViewModel(string name, string iconData, AppPage page, Action select) : ViewModelBase
{
    public string Name { get; } = name;
    public string IconData { get; } = iconData;
    public AppPage Page { get; } = page;

    [ObservableProperty]
    private bool _isActive;

    public IRelayCommand SelectCommand { get; } = new RelayCommand(select);
}

public sealed class FolderItemViewModel(string name, string info) : ViewModelBase
{
    public string Name { get; } = name;
    public string Info { get; } = info;
}

public partial class EffectRegistryItemViewModel(string name) : ViewModelBase
{
    public string Name { get; } = name;

    [ObservableProperty]
    private bool _isUsed;
}

public sealed partial class StatRowViewModel(string label, string value) : ViewModelBase
{
    public string Label { get; } = label;

    [ObservableProperty]
    private string _value = value;
}
