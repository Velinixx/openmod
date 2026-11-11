# openmod 🎤

**Free, open-source voice changer + soundboard. No subscription. No telemetry. No "upgrade to unlock."**

openmod is a Voicemod alternative built in the open: real-time voice effects,
a soundboard, and virtual audio routing — with effects, presets, and themes
that anyone can add by forking this repo.

> **Status: foundation** — the real-time audio engine works on Linux.
> Roadmap below. Windows port comes after the core is solid.

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
              ▲                          │
              └── presets (JSON) ────────┘
```

- **`openmod-core`** — audio engine: ring buffer, effect pipeline, pitch shifter, presets
- **`openmod-cli`** — test harness (the UI comes later)

## Building

```bash
cargo build
./target/debug/openmod devices      # list audio devices
./target/debug/openmod presets      # list bundled presets
./target/debug/openmod run effects/chipmunk.json
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
- [ ] **Soundboard** — clips mixed into the output chain, hotkeys
- [ ] **Virtual device** — one-click fake mic (PipeWire null-sink / Windows virtual cable) so games see *you*
- [ ] **UI** — effect browser, presets, soundboard grid (Rust core stays, UI on top)
- [ ] **Windows port** — cpal already supports WASAPI; same core, new backend
- [ ] **Voicemod import** — bring your soundboard + effect chains with you

## Contributing

Want to add an effect, preset, or soundboard? See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) —
adding an effect is ~50 lines of Rust + one JSON file.

## License

MIT
