use anyhow::{bail, Result};
use serde_json::Value;

/// A real-time audio effect.
///
/// Effects process mono f32 blocks in place. They must be cheap enough to
/// run every audio callback (no allocation in `process` after construction).
pub trait Effect: Send {
    /// Short identifier, e.g. `"gain"`. Must match the preset `"type"`.
    fn name(&self) -> &'static str;

    /// Apply the effect in place. `sample_rate` is provided on every call.
    fn process(&mut self, samples: &mut [f32], sample_rate: u32);

    /// Update a parameter by name (used by presets and, later, the UI).
    fn set_param(&mut self, key: &str, value: f32) -> Result<()>;

    /// Algorithmic latency introduced by this effect, in samples.
    fn latency_samples(&self) -> usize {
        0
    }
}

/// Build an effect from its preset `"type"` + `"params"` object.
pub fn create_effect(kind: &str, params: Option<&Value>) -> Result<Box<dyn Effect>> {
    let mut effect: Box<dyn Effect> = match kind {
        "gain" => Box::new(crate::effects::gain::Gain::default()),
        "pitch_shift" => Box::new(crate::effects::pitch::PitchShift::new()),
        other => bail!("unknown effect type: {other}"),
    };

    if let Some(Value::Object(map)) = params {
        for (key, value) in map {
            if let Some(number) = value.as_f64() {
                effect.set_param(key, number as f32)?;
            }
        }
    }

    Ok(effect)
}
