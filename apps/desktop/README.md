# openmod desktop (Avalonia)

The real UI. Everything here binds to **resource tokens** from `Themes/`, so a
theme or accent switch is a single dictionary swap — no per-view colors.

```
apps/desktop/
  App.axaml              fonts + icon geometries + merges the palette
  App.axaml.cs           loads settings, applies the palette, then shows the window
  ThemeManager.cs        runtime palette/accent swap, System-mode watching
  Themes/
    Dark.axaml           palette tokens (BgBrush, CardBrush, ...)
    Light.axaml
    Amoled.axaml         true black
    Accent.axaml         accent defaults (overridden at runtime)
  Styles/AppStyles.axaml TextBlock classes, button classes, surface states
  Models/PresetData.cs   mirror of the preset JSON schema
  Services/
    PresetLibrary.cs     lazy scan/parse + latency math (mirrors crates/core)
    SettingsStore.cs     AppSettings <-> $XDG_CONFIG_HOME/openmod/settings.json
    SystemAccent.cs      reads org.freedesktop.appearance accent-color via gdbus
  ViewModels/            MainViewModel (shell) + SettingsViewModel + card VMs
  Views/
    MainWindow.axaml     top bar / sidebar / page host / chain panel / bottom bar
    VoicesView.axaml     voice grid + community presets
    SettingsView.axaml   appearance + audio + live preview column
  Screenshot.cs          headless renderer for design iteration / CI
  SelfTest.cs            headless assertions for the settings path
```

## Build & run

.NET 10 is installed per-user in `~/.dotnet` (pacman mirrors were stale):

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
cd apps/desktop
dotnet build
dotnet run
```

## Settings

Every control writes straight through and saves immediately — there's no Apply
button to forget:

```
$XDG_CONFIG_HOME/openmod/settings.json     (~/.config/openmod/settings.json)
{
  "theme": "System",          // System | Light | Dark | Amoled
  "accent": "blush",          // preset name, or "custom"
  "accentBright": null,       // only for "custom"
  "accentDeep": null,
  "matchSystemAccent": false, // follow the desktop's accent color
  "monitor": false,
  "inputDevice": null,
  "firstRunComplete": false
}
```

- **Theme** = `System` follows the OS light/dark variant *and* keeps listening
  (`ActualThemeVariantChanged`), so switching your desktop theme re-applies ours.
- **Accent** has one token *pair*. Accent-colored **text** uses the deep tone on
  light backgrounds and the bright tone on dark/AMOLED, because a pastel accent
  is unreadable as body text on white.
- **Match system accent** shells out to `gdbus` for
  `org.freedesktop.portal.Settings.Read org.freedesktop.appearance accent-color`
  (no D-Bus library dependency). If there's no portal it refuses to pretend and
  keeps your own accent, with a note on the page.

## Headless screenshots

Renders offscreen to a PNG — no window, no screen capture, works over SSH/CI:

```bash
dotnet run -- --screenshot /tmp/openmod.png                     # voices, current theme
dotnet run -- --screenshot /tmp/openmod.png dark
dotnet run -- --screenshot /tmp/openmod.png light
dotnet run -- --screenshot /tmp/openmod.png amoled
dotnet run -- --screenshot /tmp/openmod.png dark mint            # theme + accent
dotnet run -- --screenshot /tmp/openmod.png dark mint settings   # theme + accent + page
```

Pages: `voices` · `soundboard` · `settings`.
Accents: `blush` (default) · `mint` · `sky` · `violet` · `amber` · `coral`.

Captures always start from **default** settings (never your real
`settings.json`), so a render is reproducible on any machine.

## Self-test

```bash
dotnet run -- --selftest
```

35 headless checks: theme/accent/monitor apply to live resources *and* land in
the JSON, bogus hex is rejected, a custom accent round-trips, the shell's page
switching marks exactly one nav item active, and the pill toggle's knob really
moves (layout, not just a bool). Exits non-zero on failure, so it's CI-ready.
It runs against a throwaway `XDG_CONFIG_HOME` — your real settings are never
touched.

## Lazy loading (the architecture rule)

**Startup = metadata only.** `MainViewModel` builds cards from file *names*
(one directory listing), shows the window, and only then parses preset JSON on a
worker thread (`HydrateAsync`) and fills in chips + latency. Same rule applies to
soundboard clips: filenames now, decode on play, virtualize the lists.

## Gotchas worth remembering

- `Classes="accent"` on a `Button` is **already taken** by Avalonia's Fluent theme
  (its blue system-accent primary button) and will silently win over your own
  style. That's why the solid accent button here uses `Classes="primary"`.
- Fluent's `Button` repaints `Background` on `:pointerover`/`:pressed` through
  `ContentPresenter#PART_ContentPresenter`, so any state style (`.seg.on`,
  `.nav.on`, `.toggle.on`) needs explicit overrides or the selection disappears
  under the cursor.
- A `<TextBlock>` can only carry **one** `Classes` attribute — `Classes="mono"
  ... Classes="faint"` is invalid XAML. Use `Classes="mono faint"`.
