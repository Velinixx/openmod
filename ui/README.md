# openmod UI

Design-first workspace. Mockups are generated from one palette-driven script —
**SVG is the source of truth**, PNG is for quick viewing.

```bash
python3 ui/mockup.py                                    # regenerate all SVGs
for f in ui/*.svg; do rsvg-convert -z 2 "$f" -o "${f%.svg}.png"; done
```

| File | What |
|------|------|
| `mockup-main-dark.svg` | main window — Dark |
| `mockup-main-light.svg` | main window — Light |
| `mockup-main-amoled.svg` | main window — AMOLED (true black) |
| `mockup-main-mint.svg` | accent-swap proof (Dark + mint) |
| `mockup-settings.svg` | Settings page — theme + accent picker |
| `mockup-themes.svg` | all palettes + accent presets on one sheet |
| `mockup.py` | generator (edit this, never the SVGs) |

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
- The virtual-mic card and the bottom-bar output pill are the same state: bind
  both to the output-sink view model (`openmod_vmic`).
