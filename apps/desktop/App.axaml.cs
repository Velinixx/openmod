using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenMod.Desktop.Services;
using OpenMod.Desktop.ViewModels;
using OpenMod.Desktop.Views;

namespace OpenMod.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load settings and apply the saved palette *before* the first frame so
        // there's no flash of the default theme.
        var settings = AppSettings.Load();
        ThemeManager.ApplySettings(settings);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (settings.FirstRunComplete)
            {
                desktop.MainWindow = new MainWindow { DataContext = new MainViewModel(settings) };
            }
            else
            {
                // First launch: the wizard is the app. It hands over to the shell
                // when it finishes (or when it's skipped).
                ShowWizard(desktop, settings);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Runs the wizard as its own window and swaps in the real shell afterwards.
    /// The wizard writes <c>firstRunComplete</c> itself, so quitting halfway
    /// through means it shows again next launch — which is the honest behaviour
    /// for someone who never got as far as "you're live".
    /// </summary>
    public static WizardWindow ShowWizard(IClassicDesktopStyleApplicationLifetime desktop, AppSettings settings, bool keepShell = false)
    {
        var viewModel = new WizardViewModel(settings);
        var wizard = new WizardWindow { DataContext = viewModel };

        viewModel.Finished += _ =>
        {
            if (keepShell)
            {
                // Re-run from Settings: the shell is already up and configured,
                // so just get out of its way instead of rebuilding the window.
                wizard.Close();
                return;
            }

            var shell = new MainWindow { DataContext = new MainViewModel(settings) };
            desktop.MainWindow = shell;
            shell.Show();
            wizard.Close();
        };

        if (keepShell)
        {
            // Re-run from Settings: the shell stays the main window, so the
            // wizard is a plain window on top of it.
            wizard.Show();
        }
        else
        {
            desktop.MainWindow = wizard;
        }

        return wizard;
    }
}
