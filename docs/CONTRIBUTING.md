# Contributing to openmod

Thanks for wanting to make openmod better! This project exists because
everyone should be able to fork their voice changer and make it *theirs*.

## The golden rule

**Effects, presets, and soundboards are designed to be added by pull request.**
You don't need to touch the audio engine to add a voice.

## Adding a preset (easiest — no Rust required)

1. Create `effects/your-preset.json`:

```json
{
  "name": "your preset name",
  "description": "what it sounds like",
  "effects": [
    { "type": "pitch_shift", "params": { "semitones": 5.0 } },
    { "type": "gain", "params": { "gain_db": -3.0 } }
  ]
}
```

2. Test it: `./target/debug/openmod run effects/your-preset.json`
3. Open a PR with a short description of the sound.

Available effect types: `gain`, `pitch_shift`.

## Adding an effect (Rust, ~50 lines)

1. Create `crates/core/src/effects/your-effect.rs`:

```rust
use anyhow::{bail, Result};
use crate::effect::Effect;

pub struct YourEffect {
    // params (preallocated — no allocation in process()!)
}

impl Effect for YourEffect {
    fn name(&self) -> &'static str { "your_effect" }

    fn process(&mut self, samples: &mut [f32], sample_rate: u32) {
        // mutate samples in place — this runs every audio callback
    }

    fn set_param(&mut self, key: &str, value: f32) -> Result<()> {
        match key {
            "param_name" => { /* ... */ Ok(()) }
            other => bail!("your_effect: unknown param {other}"),
        }
    }

    fn latency_samples(&self) -> usize { 0 }  // if your effect delays audio
}
```

2. Register it in `crates/core/src/effects/mod.rs`
3. Add it to the match in `crates/core/src/effect.rs` (`create_effect`)
4. Ship a preset that uses it in `effects/`
5. Test: `cargo build && ./target/debug/openmod run effects/your-preset.json`
6. Open a PR.

### Rules for `process()`

- **No allocation** — it runs in the audio callback; preallocate in `new()`
- **No blocking** — no file I/O, no locks on shared state, no sleeping
- Keep it cheap: this runs 44,100+ times per second

## Reporting bugs

Open an issue with:

- Your distro + audio stack (PipeWire/PulseAudio/JACK)
- `openmod devices` output
- What you expected vs. what happened

## Code of conduct

Be kind. We're all here because we hate subscription walls, not each other. 💕
