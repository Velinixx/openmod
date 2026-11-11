use std::fs;
use std::path::Path;

use anyhow::{Context, Result};
use serde::Deserialize;

use crate::effect::create_effect;
use crate::pipeline::Pipeline;

/// One entry in a preset file's effect chain.
#[derive(Debug, Deserialize)]
pub struct PresetEffect {
    #[serde(rename = "type")]
    pub kind: String,
    pub params: Option<serde_json::Value>,
}

/// A preset = named, forkable effect chain (the community contribution unit).
#[derive(Debug, Deserialize)]
pub struct Preset {
    pub name: Option<String>,
    pub description: Option<String>,
    pub effects: Vec<PresetEffect>,
}

impl Preset {
    pub fn from_json(json: &str) -> Result<Self> {
        serde_json::from_str(json).context("invalid preset JSON")
    }

    pub fn load(path: impl AsRef<Path>) -> Result<Self> {
        let text = fs::read_to_string(path.as_ref())
            .with_context(|| format!("cannot read preset {}", path.as_ref().display()))?;
        Self::from_json(&text)
    }

    pub fn to_pipeline(&self) -> Result<Pipeline> {
        let mut pipeline = Pipeline::new();
        for effect in &self.effects {
            pipeline.add(create_effect(&effect.kind, effect.params.as_ref())?);
        }
        Ok(pipeline)
    }
}
