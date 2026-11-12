using Avalonia;
using System;

namespace OpenMod.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // `--selftest` asserts the settings path headlessly (CI friendly)
        if (args.Length >= 1 && args[0] == "--selftest")
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }

        // `--screenshot <path> [theme] [accent] [page] [height]` renders offscreen
        // (height lets long pages like Settings be captured in one frame)
        if (args.Length >= 2 && args[0] == "--screenshot")
        {
            Screenshot.Capture(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null,
                args.Length >= 5 ? args[4] : null,
                args.Length >= 6 && int.TryParse(args[5], out var h) ? h : 800);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
