use anyhow::{bail, Result};

use crate::effect::Effect;

/// Granular pitch shifter.
///
/// Two overlapping grains (Hann-windowed, 50% offset) read from a history
/// buffer at `ratio` x real-time rate. Crossfading two grains keeps the
/// output continuous while changing pitch without changing duration.
///
/// Trade-offs (foundation-stage, documented honestly):
/// - Algorithmic latency = `GRAIN * ratio` samples (~21ms @48k, ratio 1.0).
/// - Linear interpolation (no formant preservation yet — that's a v0.2 effect).
/// - Ratio clamped to [0.25, 4.0] so the history buffer always covers it.
const GRAIN: usize = 1024;
const CAPACITY: usize = 16384;
const MIN_RATIO: f32 = 0.25;
const MAX_RATIO: f32 = 4.0;

struct Head {
    phase: usize,
    read_abs: f64,
}

pub struct PitchShift {
    ratio: f64,
    buffer: Vec<f32>,
    hann: Vec<f32>,
    written: u64,
    heads: [Head; 2],
}

impl PitchShift {
    pub fn new() -> Self {
        let hann = (0..GRAIN)
            .map(|i| 0.5 * (1.0 - (2.0 * std::f32::consts::PI * i as f32 / GRAIN as f32).cos()))
            .collect();
        Self {
            ratio: 1.0,
            buffer: vec![0.0; CAPACITY],
            hann,
            written: 0,
            heads: [
                Head {
                    phase: 0,
                    read_abs: 0.0,
                },
                Head {
                    phase: GRAIN / 2,
                    read_abs: 0.0,
                },
            ],
        }
    }

    fn delay(&self) -> f64 {
        GRAIN as f64 * self.ratio
    }
}

#[inline]
fn read_buffer(buffer: &[f32], abs: f64) -> f32 {
    if abs < 0.0 {
        return 0.0;
    }
    let pos = abs % CAPACITY as f64;
    let i0 = pos as usize;
    let i1 = (i0 + 1) % CAPACITY;
    let frac = (pos - i0 as f64) as f32;
    buffer[i0] * (1.0 - frac) + buffer[i1] * frac
}

impl Default for PitchShift {
    fn default() -> Self {
        Self::new()
    }
}

impl Effect for PitchShift {
    fn name(&self) -> &'static str {
        "pitch_shift"
    }

    fn process(&mut self, samples: &mut [f32], _sample_rate: u32) {
        let delay = self.delay();
        let ratio = self.ratio;
        for i in 0..samples.len() {
            self.buffer[self.written as usize % CAPACITY] = samples[i];

            // advance grain phases (mutable head state only)
            for head in &mut self.heads {
                head.phase += 1;
                if head.phase >= GRAIN {
                    // Grain finished: jump back so the whole grain reads
                    // history that has already been written.
                    head.phase = 0;
                    head.read_abs = self.written as f64 - delay;
                }
            }

            // crossfade the two grains (immutable reads only)
            let mut out = 0.0f32;
            for head in &self.heads {
                out += read_buffer(&self.buffer, head.read_abs) * self.hann[head.phase];
            }

            // advance read positions for the next sample
            for head in &mut self.heads {
                head.read_abs += ratio;
            }

            samples[i] = out;
            self.written += 1;
        }
    }

    fn set_param(&mut self, key: &str, value: f32) -> Result<()> {
        match key {
            "semitones" => {
                let ratio = 2f32.powf(value / 12.0);
                self.ratio = f64::from(ratio.clamp(MIN_RATIO, MAX_RATIO));
            }
            "ratio" => self.ratio = f64::from(value.clamp(MIN_RATIO, MAX_RATIO)),
            other => bail!("pitch_shift: unknown param {other}"),
        }
        Ok(())
    }

    fn latency_samples(&self) -> usize {
        self.delay() as usize
    }
}
