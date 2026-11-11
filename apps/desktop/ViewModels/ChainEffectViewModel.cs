using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenMod.Desktop.ViewModels;

/// <summary>
/// One effect in the active chain. Slider geometry is pre-computed so the view
/// needs no converters: <see cref="FillWidth"/> and <see cref="KnobX"/> are
/// pixel values inside a <see cref="TrackWidth"/>-wide track.
/// </summary>
public partial class ChainEffectViewModel : ViewModelBase
{
    public const double TrackWidth = 232;
    private const double KnobSize = 13;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _paramLabel;

    [ObservableProperty]
    private string _paramValue;

    [ObservableProperty]
    private double _fillWidth;

    [ObservableProperty]
    private double _knobX;

    public ChainEffectViewModel(string name, string paramLabel, string paramValue, double fraction)
    {
        _name = name;
        _paramLabel = paramLabel;
        _paramValue = paramValue;
        SetFraction(fraction);
    }

    public void SetFraction(double fraction)
    {
        var f = System.Math.Clamp(fraction, 0.0, 1.0);
        FillWidth = TrackWidth * f;
        KnobX = TrackWidth * f - KnobSize / 2;
    }
}
