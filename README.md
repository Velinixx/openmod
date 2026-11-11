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
- [ ] **UI** — Settings page (theme/accent picker), Soundboard page, module system, Zen-style first-launch wizard
- [ ] **Windows port** — cpal already supports WASAPI; same core, new backend
- [ ] **Voicemod import** — bring your soundboard + effect chains with you

## Contributing

Want to add an effect, preset, or soundboard? See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) —
adding an effect is ~50 lines of Rust + one JSON file.

## License

MIT
