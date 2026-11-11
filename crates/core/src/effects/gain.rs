use anyhow::{bail, Result};

use crate::effect::Effect;

/// Simple gain stage. Params: `gain_db` (dB) or `gain` (linear).
#[derive(Default)]
pub struct Gain {
    linear: f32,
}

impl Gain {
    pub fn from_db(db: f32) -> Self {
        Self {
            linear: 10f32.powf(db / 20.0),
        }
    }
}

impl Effect for Gain {
    fn name(&self) -> &'static str {
        "gain"
    }

    fn process(&mut self, samples: &mut [f32], _sample_rate: u32) {
        let g = self.linear;
        if g != 1.0 {
            for s in samples.iter_mut() {
                *s *= g;
            }
        }
    }

    fn set_param(&mut self, key: &str, value: f32) -> Result<()> {
        match key {
            "gain_db" => self.linear = 10f32.powf(value / 20.0),
            "gain" => self.linear = value,
            other => bail!("gain: unknown param {other}"),
        }
        Ok(())
    }
}
