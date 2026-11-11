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
        // `--screenshot <path> [theme]` renders offscreen (design iteration / CI)
        if (args.Length >= 2 && args[0] == "--screenshot")
        {
            Screenshot.Capture(args[1], args.Length >= 3 ? args[2] : null, args.Length >= 4 ? args[3] : null);
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
