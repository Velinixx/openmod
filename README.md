# openmod 🎤

**Free, open-source voice changer + soundboard. No subscription. No telemetry. No "upgrade to unlock."**

openmod is a Voicemod alternative built in the open: real-time voice effects,
a soundboard, and virtual audio routing — with effects, presets, and themes
that anyone can add by forking this repo.

> **Status: early alpha** — the real-time audio engine and the desktop UI both
> run on Linux. Roadmap below. Windows port comes after the core is solid.

## Why?

Voice changers today force you into subscriptions, hide features behind paywalls,
and treat your soundboard like rented land. openmod is:

- ✅ **Free forever** — MIT licensed
- ✅ **Forkable** — effects and presets are just JSON + a Rust file
- ✅ **Yours** — no account, no phone-home, no upsell screen on launch
- ✅ **Low-latency** — real-time DSP in Rust, no garbage collector pauses

### The gap this fills

Voicemod ships Windows, macOS, iOS and Android — **there is no Linux client**,
and no serious open alternative either. On Linux, openmod doesn't even need a
driver: PipeWire null sinks give you a virtual microphone for free, no
elevation, no reboot. Windows gets the same result through an optional
one-press virtual-cable install in the first-run wizard.

## How it works

```
mic ──▶ [ mono ring buffer ] ──▶ [ effect pipeline ] ──▶ output device
              ▲                          │                    │
              └── presets (JSON) ────────┘                    └──▶ (--vmic) openmod_vmic ──▶ games
```

- **`openmod-core`** — audio engine: ring buffer, effect pipeline, pitch shifter, presets, soundboard
- **`openmod-cli`** — test harness for the engine
- **`apps/desktop`** — the real UI (Avalonia / C#), themed with swappable palette + accent tokens

## Building

```bash
cargo build
./target/debug/openmod devices      # list audio devices
./target/debug/openmod presets      # list bundled presets
./target/debug/openmod run effects/chipmunk.json
./target/debug/openmod run --vmic effects/chipmunk.json   # publish to a virtual mic

./target/debug/openmod soundboard list                   # clips (file names only)
./target/debug/openmod soundboard list --probe           # + durations from headers
./target/debug/openmod soundboard play airhorn --vmic    # decode one clip -> virtual mic
```

The desktop app:

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
cd apps/desktop && dotnet run

dotnet run -- --selftest                                   # 35 headless checks
dotnet run -- --screenshot out.png dark mint settings       # theme + accent + page
```

## Soundboard

Audio files in `soundboards/` (`.wav` `.mp3` `.ogg` `.flac` `.m4a` `.aac`).
Metadata is **lazy**: startup does one directory listing, durations come from
container headers, and a clip is only decoded when it actually plays. See
[soundboards/README.md](soundboards/README.md).

### The virtual mic (`--vmic`)

`--vmic` creates (or reuses) a PipeWire/PulseAudio null sink called `openmod_vmic`
and routes openmod's processed output into it. Any app — games, VRChat, Discord —
that selects **"Monitor of openmod_Virtual_Mic"** as its microphone hears your
processed voice. No cables, no extra software:

```bash
openmod run --vmic effects/deep-voice.json
# output routed -> openmod_vmic
#   set your mic to "Monitor of openmod_Virtual_Mic" in games/apps
```

## Presets

Presets live in `effects/*.json` and are the unit of community contribution:

```json
{
  "name": "chipmunk",
  "description": "Classic high-pitch voice",
  "effects": [
    { "type": "pitch_shift", "params": { "semitones": 7.0 } },
    { "type": "gain", "params": { "gain_db": 0.0 } }
  ]
}
```

Bundled: `passthrough`, `chipmunk`, `deep-voice`.

## Roadmap

- [x] **Foundation** — real-time pipeline, pitch shifter, presets, CLI harness
- [ ] **Effects v0.2** — formant shift, reverb, distortion, ring mod, voice vault (saved voices)
- [x] **Soundboard library** — lazy scan (names only), header-only duration probe, decode-on-play, `play --vmic`
- [ ] **Soundboard mixing** — clips mixed into the live output chain + global hotkeys
- [x] **Virtual device** — `openmod run --vmic` (PipeWire null-sink fake mic, auto-created + routed); Windows virtual cable later
- [x] **UI foundation** — Avalonia main window: voice grid, effect chain, virtual-mic status, theme + accent tokens (dark / light / AMOLED, 6 accent presets, custom)
- [x] **Settings page** — theme picker (System / Light / Dark / AMOLED), 6 accent presets + custom hex, follow-desktop-accent via the XDG portal, audio rows, live preview column; every change writes `~/.config/openmod/settings.json` immediately
- [x] **First-launch wizard** — welcome → theme + accent (3-up live preview: dark / light / AMOLED) → virtual mic → import → done. Linux shows `openmod_vmic` ready; Windows offers an **optional one-press VB-CABLE install** (ALVR-style, newest package scraped from vb-audio.com, visible installer, always skippable). Re-runnable from Settings → setup
- [ ] **UI** — Soundboard page (library is ready)
- [ ] **Suggestions** — optional, off-by-default, local-only tagging of your own clips (sound effect / voice line / music + pitch range) to suggest hotkeys, effect chains, duplicates and trims. Never uploads, never applies without asking, dismissal remembered per file hash. Spec in `ui/README.md`
- [ ] **Community** — browse community sounds/presets and **export them in a format you pick** (wav / mp3 / flac; encoders, not decoders, are the limit). Adding your own clip offers **private (default) or publish-as-PR**, with **hashtags** (`#game`, `#sfx`, …) and one search syntax for both local and community (`#tag` scopes to tags, plain text hits titles + tags)
- [ ] **Community `for you`** — search plus a locally-ranked recommendation view. Content-based over the community manifest against your likes, on your machine: no account, no profiling, no "people like you" — and it says so
- [ ] **Layout** — drag panels around and resize them, with presets (Voicemod-familiar / Zen / Compact)
- [ ] **Modules** — composable widgets (media player, heart rate, chatbox)
- [ ] **Windows port** — cpal already supports WASAPI; same core, new backend
- [ ] **Voicemod import** — bring your soundboard + effect chains with you

## Contributing

Want to add an effect, preset, or soundboard? See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) —
adding an effect is ~50 lines of Rust + one JSON file.

## License

MIT
