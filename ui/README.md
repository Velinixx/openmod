# openmod UI

Design-first workspace. Mockups are generated from one palette-driven script —
**SVG is the source of truth**, PNG is for quick viewing.

```bash
python3 ui/mockup.py                                    # regenerate all SVGs
for f in ui/*.svg; do rsvg-convert -z 2 "$f" -o "${f%.svg}.png"; done
python3 ui/wizard_sheet.py                              # wizard contact sheet
rsvg-convert -z 1 ui/mockup-wizard-sheet.svg -o ui/mockup-wizard-sheet.png
```

The welcome copy is a parameter of `draw_wizard_welcome(headline, tagline)`.
Shipped wording is lowercase — it matches every other headline in the flow
(`you're live`), and the product name is lowercase everywhere else too.

| File | What |
|------|------|
| `mockup-main-dark.svg` | main window — Dark |
| `mockup-main-light.svg` | main window — Light |
| `mockup-main-amoled.svg` | main window — AMOLED (true black) |
| `mockup-main-mint.svg` | accent-swap proof (Dark + mint) |
| `mockup-settings.svg` | Settings page — theme + accent picker |
| `mockup-wizard-1-welcome.svg` | First launch · step 1 — welcome |
| `mockup-wizard-2-appearance.svg` | First launch · step 2 — theme + accent (dark) |
| `mockup-wizard-2-appearance-light.svg` | same step, light + violet |
| `mockup-wizard-3-vmic-windows.svg` | First launch · step 3 — virtual mic (**one-click cable install**) |
| `mockup-wizard-3-vmic-linux.svg` | same step on Linux — already done by PipeWire |
| `mockup-wizard-4-import.svg` | First launch · step 4 — import existing folders |
| `mockup-wizard-5-done.svg` | First launch · step 5 — you're live |
| `mockup-wizard-sheet.svg` | all five wizard steps on one contact sheet |
| `mockup-themes.svg` | all palettes + accent presets on one sheet |
| `mockup.py` | generator (edit this, never the SVGs) |
| `wizard_sheet.py` | composes `mockup-wizard-sheet.svg` from the 2x wizard PNGs |

## Stack (decided 2026-11-11)

**Avalonia UI (C#)** — cross-platform, XAML ≈ WPF, so the same UI ships to
Linux first and Windows later without a rewrite. Rust core stays untouched
behind a thin interop layer.

## Main window layout spec

```
┌─────────────────────────────────────────────────────────────────────┐
│ TOP BAR 56                                                          │
├──────────┬──────────────────────────────────────┬───────────────────┤
│ SIDEBAR  │  CONTENT                             │  CHAIN PANEL 312  │
│ 208      │  (flex)                              │                   │
│          │                                      │                   │
├──────────┴──────────────────────────────────────┴───────────────────┤
│ BOTTOM BAR 64                                                       │
└─────────────────────────────────────────────────────────────────────┘
```

| Region      | Size      | Contents |
|-------------|-----------|----------|
| Top bar     | 1280×56   | logo + wordmark, centered `VOICE ON` toggle, latency chip, theme + settings |
| Sidebar     | 208×680   | nav (Voices / Soundboard / Settings), effect registry, folders, virtual-mic card, project footer |
| Content     | flex      | `My Voices` header + search + new, 3×2 voice cards, community presets list |
| Chain panel | 312×680   | active chain name + on/off, draggable effect cards with sliders, stats, save/export |
| Bottom bar  | 1280×64   | input device, **output = openmod_vmic**, IN/OUT meters, volume, monitor toggle, stop |

### Voice card (224×152)

Icon badge 36×36 → name 14.5 semibold → chain chips (`pitch +7.0`, `gain 0.0`)
→ latency + `edit ›`. Active card: accent border + glow + `● LIVE` pill.
Last slot in the grid is a dashed `New voice` card.

### Chain effect card (264×112)

Drag dots · effect name (mono) · remove × · param label + value · slider ·
"drag to reorder" hint. Ordered stack = pipeline order (matches `Vec<Effect>`).

## Theme system

**Three palettes, one accent token pair.** Every screen is drawn from tokens
only — no per-screen color overrides. Changing the accent re-themes the whole
app (see `mockup-main-mint.svg`).

| Token        | Dark      | Light     | AMOLED    |
|--------------|-----------|-----------|-----------|
| `bg`         | `#23272F` | `#F2F5F9` | `#000000` |
| `panel`      | `#1B1F26` | `#FFFFFF` | `#000000` |
| `card`       | `#2C323C` | `#FFFFFF` | `#0C0C0C` |
| `deep`       | `#262B34` | `#EDF1F6` | `#141414` |
| `track`      | `#3A4250` | `#DCE3EC` | `#262626` |
| `border`     | `#333B47` | `#E1E7EF` | `#1E1E1E` |
| `text`       | `#ECEFF4` | `#161B22` | `#FFFFFF` |
| `textSoft`   | `#AEB8C6` | `#4A5563` | `#B4B4B4` |
| `textMuted`  | `#6E7A8A` | `#7B8794` | `#6E6E6E` |
| `textFaint`  | `#4E5764` | `#A7B1BD` | `#3D3D3D` |
| `onAccent`   | `#2A0E1A` | `#2A0E1A` | `#12060C` |

**Theme modes:** `System` · `Light` · `Dark` · `AMOLED`.

- **System** follows the OS light/dark preference, and (if enabled) the OS
  accent color.
- **AMOLED** is the dark palette with `bg`/`panel` at pure `#000000` and
  minimal elevation — made for OLED panels.

### Accent

One token **pair** (`accent`, `accentDeep`) drives gradients, active states,
meters, the voice toggle, and LIVE pills.

| Preset | `accent` | `accentDeep` |
|--------|----------|--------------|
| blush (default) | `#FF99CC` | `#E91E63` |
| mint  | `#7FE0C3` | `#0FA97F` |
| sky   | `#8CC8FF` | `#2E7DD1` |
| violet| `#C3A6FF` | `#7C4DFF` |
| amber | `#FFCF7A` | `#E08C00` |
| coral | `#FFA08C` | `#E05A3F` |

Rules:

- Text **on** an accent surface uses `onAccent`.
- Accent-colored **text** uses `accentDeep` on the light theme (contrast) and
  `accent` on dark/AMOLED — one helper (`Canvas.ink`) encodes this.
- Gradients are always `accent → accentDeep` (diagonal for fills, horizontal
  for meters).
- Custom accents are allowed (color picker) — any pair works, with the same
  contrast rule.

Type: **Cantarell** (UI) + **DejaVu Sans Mono** (params, device names, file
paths). Radii: cards 12, inputs/buttons 9–10, pills full. Hairlines 1px.

## First-launch wizard (mockup only)

Zen-Browser-style: a full-window overlay (no sidebar, no chain panel), one idea
per step, one primary action per step, `skip for now` always available.

```
welcome ──▶ appearance ──▶ virtual mic ──▶ import ──▶ you're live
                              │
                              └── the only step that differs per platform
```

| Step | Content | Why it exists |
|------|---------|---------------|
| 1 Welcome | logo, **"welcome to openmod"** / **"no hassle · no subscriptions · all freedom"**, the three promises (free / offline / your files), one `Get started` | Zen's calm front door. No account, no email, no plan picker — nothing to decline |
| 2 Appearance | 4 theme cards with **live mini previews** + accent swatches + a live preview strip | the wizard recolors itself — it *is* the preview, using the real token swap |
| 3 Virtual mic | Windows: optional one-press **VB-CABLE** install · Linux: `openmod_vmic` already ready | the only genuinely hard requirement; see below |
| 4 Import | presets / soundboard / other voice changers, with counts and paths | migrating shouldn't mean re-downloading your library |
| 5 Done | `openmod_vmic` + the monitor name, `Open openmod` | ends on the one fact they need at the moment they need it |

Footer = progress dots + `Back` + primary action. Steps 1 and 5 hide the footer
action because the hero button *is* the action (two "Get started" buttons is a
mistake worth not shipping).

### Step 3 — the optional audio-driver add-on (ALVR-style)

On **Linux** there is nothing to install: PipeWire null sinks are free, so the
step just reports `openmod_vmic` as ready and offers `Test it` / `Recreate`.

On **Windows** a virtual cable is a real driver, and openmod cannot be a
microphone without one. So the step offers to do it *for* you, once, opt-in:

- **Primary:** `Install it for me` — downloads the **official** installer from
  `vb-audio.com` at runtime and runs it with the flags VB-Audio documents.
  Nothing is bundled in our repo, nothing is patched, no repackaging.
- **Secondary:** `open vb-audio.com` — the do-it-yourself path, for people who
  don't want an app downloading an installer.
- **Alternatives:** `I already have a virtual cable` (detect it — VoiceMeeter,
  VB-CABLE, Voicemod's own, anything with a capture endpoint) and
  `skip this for now` (openmod still runs, you're just not a mic yet).
- **Honesty in the UI:** VB-CABLE is donationware (free to use, asks for a
  donation), the install needs elevation once and usually a reboot, and the
  step says so *before* the click — not in a changelog.
- **Never silently:** no background install, no "recommended" pre-ticked, no
  nagging later. If they skip, the main window keeps a quiet hint in the
  virtual-mic card.

This is exactly what ALVR does for its audio driver, and it's the difference
between a first-run that works and a first-run that ends in a support thread.

## Backlog ideas (captured, not designed yet)

**Community export.** The community tab should let you *take* a sound, not just
play it: pick a format (`wav` / `mp3` / `flac`) and export. Design constraints:

- The community index can be a JSON manifest in this repo
  (`community/sounds.json`: name, author, url, sha256, license) — no server, and
  it stays forkable. Only `CC0` / `CC-BY` entries get listed.
- Download once, cache the original, convert on export. Never re-encode the
  cached master.
- **Formats are limited by encoders, not decoders.** symphonia decodes
  wav/mp3/ogg/flac/m4a/aac, but Rust encoders are uneven: wav is trivial, mp3
  via `mp3lame-encoder`, flac via `flacenc`; Vorbis/Opus encoders are immature
  or absent. So ship `wav` / `mp3` / `flac`, and be honest about `ogg` instead
  of quietly producing a `.ogg` that's really something else.
- Export options: mono/stereo, sample rate, bitrate. The voice pipeline is mono,
  so stereo means duplicated channels unless the source was stereo.

**Draggable + resizable layout.** Users should be able to move the mic card to
the top, move the nav rail to the right, and drag panel edges for a compact or
roomy window. Don't hand-roll a docking framework:

- Model the shell as a few **dockable regions** (nav, main content, right panel,
  bottom bar) each with `side` / `size` / `visible` / `order`, plus the module
  list from the module-system idea — one `layout.json`.
- `GridSplitter` for resize, pointer-drag for reorder, and **3 presets**
  (Voicemod-familiar, Zen, Compact icon rail) so nobody has to build a layout
  from scratch.
- If floating/detachable panels ever become a requirement, adopt
  **Dock.Avalonia** (MIT) rather than writing one.
- Default layout must stay identical to today's screenshots, and this ships
  *after* onboarding — a wizard that ends in "now rearrange everything" is not
  calm.

## Notes for implementation (Avalonia)

- One `ResourceDictionary` per palette (`Dark.axaml`, `Light.axaml`,
  `Amoled.axaml`) exposing the tokens above as `SolidColorBrush` /
  `LinearGradientBrush` resources; views only reference `{DynamicResource …}`.
- Theme mode → `Application.RequestedThemeVariant`
  (`ThemeVariant.Default` = System, `.Light`, `.Dark`); AMOLED = Dark variant
  with the AMOLED dictionary.
- Accent → override `AccentBrush` / `AccentDeepBrush` at runtime; derive the
  gradient brush and `OnAccentBrush` from them. "Match system accent" reads the
  desktop accent (Linux: `org.freedesktop.appearance accent-color` portal;
  Windows: DWM colorization) and disables the manual swatches while on.
- Stats come straight from core counters (`queued`, `underruns`, `overruns`)
  plus `latency_samples()`.
- Sidebar nav is an `ItemsControl`; only the Voices page and Settings are
  designed — Soundboard is a placeholder for now.
- The wizard is a separate window/lifetime, not a page: it runs when
  `settings.firstRunComplete` is false and writes that flag on finish *or* skip.
  Every step's state goes through the same `ThemeManager` / settings store the
  real app uses, so step 2 needs no preview-specific code.
- The virtual-mic card and the bottom-bar output pill are the same state: bind
  both to the output-sink view model (`openmod_vmic`).
