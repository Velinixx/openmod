using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenMod.Desktop.Services;
using OpenMod.Desktop.ViewModels;
using OpenMod.Desktop.Views;

namespace OpenMod.Desktop;

/// <summary>
/// Renders the app offscreen to a PNG — used for design iteration and CI.
/// `openmod-ui --screenshot out.png [system|light|dark|amoled] [blush|mint|sky|violet|amber|coral] [page]`
/// where page is voices|soundboard|settings, or a wizard step: wizard1..wizard5
/// (add "win" — wizard3win — to force the Windows branch on Linux).
/// </summary>
static class Screenshot
{
    public static void Capture(string path, string? theme = null, string? accent = null, string? page = null,
        int height = 800)
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        // Captures always start from defaults (never the user's real settings),
        // so a render is reproducible on any machine.
        ThemeManager.ApplySettings(new AppSettings());

        if (!string.IsNullOrEmpty(theme) || !string.IsNullOrEmpty(accent))
        {
            ThemeManager.Apply(theme, accent);
        }

        if (page is not null && page.StartsWith("wizard", StringComparison.OrdinalIgnoreCase))
        {
            CaptureWizard(path, page, height);
            return;
        }

        var vm = new MainViewModel(new AppSettings());

        if (!string.IsNullOrEmpty(page)
            && Enum.TryParse<AppPage>(page, true, out var target))
        {
            vm.ShowPage(target);
        }

        var window = new MainWindow { DataContext = vm, Width = 1280, Height = height };
        window.Show();

        // The view model loads metadata first, then parses preset JSON in the
        // background. Give it a moment so the capture shows hydrated content.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (vm.IsHydrated)
            {
                break;
            }

            Thread.Sleep(10);
        }

        Save(window, path);
    }

    /// <summary>Renders one wizard step at 1280x800.</summary>
    private static void CaptureWizard(string path, string page, int height)
    {
        var forceWindows = page.Contains("win", StringComparison.OrdinalIgnoreCase);
        var digits = new string(page.Where(char.IsDigit).ToArray());
        var step = int.TryParse(digits, out var parsed) ? Math.Clamp(parsed, 1, 5) : 1;

        var vm = new WizardViewModel(new AppSettings(), forceWindows ? true : null)
        {
            StepIndex = step - 1,
        };

        var window = new WizardWindow { DataContext = vm, Width = 1280, Height = height };
        window.Show();

        // Carousel lays the selected page out during measure, so give it a couple
        // of passes before grabbing the frame.
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Save(window, path);
    }

    private static void Save(Window window, string path)
    {
        // let layout/measure passes settle before grabbing the frame
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            Console.Error.WriteLine("screenshot failed: no frame rendered");
            Environment.Exit(1);
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        frame.Save(path);
        Console.WriteLine($"wrote {path}");
    }
}
