# openmod desktop (Avalonia)

The real UI. Everything here binds to **resource tokens** from `Themes/`, so a
theme or accent switch is a single dictionary swap — no per-view colors.

```
apps/desktop/
  App.axaml              fonts + icon geometries + merges the palette
  ThemeManager.cs        runtime palette/accent swap, System-mode watching
  Themes/
    Dark.axaml           palette tokens (BgBrush, CardBrush, ...)
    Light.axaml
    Amoled.axaml         true black
    Accent.axaml         accent defaults (overridden at runtime)
  Styles/AppStyles.axaml TextBlock classes, button classes, surface states
  Models/PresetData.cs   mirror of the preset JSON schema
  Services/PresetLibrary.cs  lazy scan/parse + latency math (mirrors crates/core)
  ViewModels/            MainViewModel + card view models
  Views/MainWindow.axaml the window
  Screenshot.cs          headless renderer for design iteration / CI
```

## Build & run

.NET 10 is installed per-user in `~/.dotnet` (pacman mirrors were stale):

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
cd apps/desktop
dotnet build
dotnet run
```

## Headless screenshots

Renders offscreen to a PNG — no window, no screen capture, works over SSH/CI:

```bash
dotnet run -- --screenshot /tmp/openmod.png              # current theme
dotnet run -- --screenshot /tmp/openmod.png dark
dotnet run -- --screenshot /tmp/openmod.png light
dotnet run -- --screenshot /tmp/openmod.png amoled
dotnet run -- --screenshot /tmp/openmod.png dark mint     # theme + accent
```

Accents: `blush` (default) · `mint` · `sky` · `violet` · `amber` · `coral`.

Latest renders live in `ui/real/`.

## Lazy loading (the architecture rule)

**Startup = metadata only.** `MainViewModel` builds cards from file *names*
(one directory listing), shows the window, and only then parses preset JSON on a
worker thread (`HydrateAsync`) and fills in chips + latency. Same rule applies to
soundboard clips: filenames now, decode on play, virtualize the lists.

## Gotcha worth remembering

`Classes="accent"` on a `Button` is **already taken** by Avalonia's Fluent theme
(its blue system-accent primary button) and will silently win over your own
style. That's why the solid accent button here uses `Classes="primary"`.
