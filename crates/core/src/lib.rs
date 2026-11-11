//! openmod-core: real-time audio pipeline and voice effects.
//!
//! Architecture (Linux-first, Windows later):
//!   mic (cpal input) -> mono ring buffer -> effect pipeline -> output device
//!
//! The pipeline is deliberately simple: a `Vec<Box<dyn Effect>>` processed
//! in order over mono f32 blocks. Stereo is downmixed on input and
//! re-duplicated on output (voice is mono anyway).

pub mod effect;
pub mod effects;
pub mod pipeline;
pub mod preset;
pub mod ring;

pub use effect::Effect;
pub use pipeline::Pipeline;
pub use preset::Preset;
