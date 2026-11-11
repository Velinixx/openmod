using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using OpenMod.Desktop.ViewModels;
using OpenMod.Desktop.Views;

namespace OpenMod.Desktop;

/// <summary>
/// Renders the app offscreen to a PNG — used for design iteration and CI.
/// `openmod-ui --screenshot out.png [system|light|dark|amoled] [blush|mint|sky|violet|amber|coral]`
/// </summary>
static class Screenshot
{
    public static void Capture(string path, string? theme = null, string? accent = null)
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        if (!string.IsNullOrEmpty(theme))
        {
            ThemeManager.Apply(theme, accent);
        }

        var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm, Width = 1280, Height = 800 };
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
