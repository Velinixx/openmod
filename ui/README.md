# openmod UI

Design-first workspace. The main-window mockup lives here as **SVG (source of
truth)** + PNG (for quick viewing).

```bash
python3 ui/mockup_main.py                      # regenerate ui/mockup-main.svg
rsvg-convert -z 2 ui/mockup-main.svg -o ui/mockup-main.png
```

- `mockup-main.svg` / `.png` — main window (1280×800)
- `mockup_main.py` — generator script (edit this, not the SVG)

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

| Region      | Size                          | Contents |
|-------------|-------------------------------|----------|
| Top bar     | 1280×56                       | logo + wordmark, centered `VOICE ON` toggle, latency chip, settings |
| Sidebar     | 208×680                       | nav (Voices / Soundboard / Settings), effect registry, folders, virtual-mic card, project footer |
| Content     | flex (~760×680)               | `My Voices` header + search + new, 3×2 voice cards, community presets list |
| Chain panel | 312×680                       | active chain name + on/off, draggable effect cards with sliders, stats, save/export |
| Bottom bar  | 1280×64                       | input device, **output = openmod_vmic**, IN/OUT meters, volume, monitor toggle, stop |

### Voice card (224×152)

Icon badge 36×36 → name 14.5 semibold → chain chips (`pitch +7.0`, `gain 0.0`)
→ latency + `edit ›`. Active card: pink border + glow + `● LIVE` pill.
Last slot in the grid is a dashed `New voice` card.

### Chain effect card (264×112)

Drag dots · effect name (mono) · remove × · param label + value · slider ·
"drag to reorder" hint. Ordered stack = pipeline order (matches `Vec<Effect>`).

## Theme tokens

| Token        | Value     | Use |
|--------------|-----------|-----|
| `bg`         | `#23272F` | window background |
| `panel`      | `#1B1F26` | top bar, sidebar, chain panel, bottom bar |
| `card`       | `#2C323C` | cards, inputs |
| `cardDeep`   | `#262B34` | chips, inset surfaces |
| `track`      | `#3A4250` | slider tracks, dividers |
| `border`     | `#333B47` | 1px hairlines |
| `text`       | `#ECEFF4` | primary |
| `textSoft`   | `#AEB8C6` | secondary |
| `textMuted`  | `#6E7A8A` | labels |
| `accent`     | `#FF99CC` | blush — active states, LIVE, meters |
| `accentDeep` | `#E91E63` | gradients, stop button |

Type: **Cantarell** (UI) + **DejaVu Sans Mono** (params, device names, file
paths). Radii: cards 12, inputs/buttons 9–10, pills full. Hairlines 1px.

## Notes for implementation

- Sidebar nav is a `ListBox`-ish `ItemsControl`; only one page is designed for
  now (Voices) — Soundboard/Settings are placeholders.
- The virtual-mic card and the bottom-bar output pill are the same state:
  bind both to the "output sink" view model (`openmod_vmic`).
- Stats come straight from the core counters (`queued`, `underruns`,
  `overruns`) + `latency_samples()`.
- Community presets list is a placeholder for a future curated feed
  (`browse on GitHub ↗` → repo URL).
