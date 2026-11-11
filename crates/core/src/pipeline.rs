use crate::effect::Effect;

/// Ordered chain of effects processed over mono f32 blocks.
#[derive(Default)]
pub struct Pipeline {
    effects: Vec<Box<dyn Effect>>,
}

impl Pipeline {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn add(&mut self, effect: Box<dyn Effect>) {
        self.effects.push(effect);
    }

    pub fn process(&mut self, samples: &mut [f32], sample_rate: u32) {
        for effect in &mut self.effects {
            effect.process(samples, sample_rate);
        }
    }

    /// Total algorithmic latency of the chain, in samples.
    pub fn latency_samples(&self) -> usize {
        self.effects.iter().map(|e| e.latency_samples()).sum()
    }

    pub fn effect_names(&self) -> Vec<&'static str> {
        self.effects.iter().map(|e| e.name()).collect()
    }

    pub fn is_empty(&self) -> bool {
        self.effects.is_empty()
    }
}
