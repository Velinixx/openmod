using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OpenMod.Desktop.ViewModels;

namespace OpenMod.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _wired;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Settings → setup re-opens the wizard; the footnote in the wizard's
        // last step promises exactly this.
        if (_wired is not null)
        {
            _wired.Settings.SetupRequested -= OpenSetup;
        }

        _wired = DataContext as MainViewModel;
        if (_wired is not null)
        {
            _wired.Settings.SetupRequested += OpenSetup;
        }
    }

    private void OpenSetup()
    {
        if (_wired is null)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            App.ShowWizard(desktop, _wired.AppSettings, keepShell: true);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_wired is not null)
        {
            _wired.Settings.SetupRequested -= OpenSetup;
            _wired = null;
        }

        base.OnClosed(e);
    }
}
