//! Soundboard clips: **lazy metadata, decode on demand**.
//!
//! The same rule the UI uses for presets applies here, because a soundboard is
//! where the file counts get big (hundreds of memes):
//!
//! 1. [`Soundboard::scan`] — one directory listing. File names, formats, sizes.
//!    No file is opened, so 500 clips costs one `readdir`.
//! 2. [`Soundboard::probe`] — reads *only* the container header for a duration.
//!    Cheap, but still skipped at startup.
//! 3. [`Soundboard::load`] — full decode into mono f32. Only when playing.
//!
//! Everything is downmixed to mono because the voice pipeline is mono
//! (see `pipeline.rs`).

use std::fs::File;
use std::path::{Path, PathBuf};
use std::time::Duration;

use anyhow::{Context, Result};
use symphonia::core::audio::SampleBuffer;
use symphonia::core::codecs::DecoderOptions;
use symphonia::core::errors::Error as SymphoniaError;
use symphonia::core::formats::FormatOptions;
use symphonia::core::io::MediaSourceStream;
use symphonia::core::meta::MetadataOptions;
use symphonia::core::probe::Hint;

/// Extensions we can decode, given the symphonia features we enable.
///
/// Note: **opus is not supported** — symphonia 0.5 has no opus decoder.
/// `.opus` files are skipped by [`Soundboard::scan`] rather than listed and
/// then failing on play.
pub const SUPPORTED_EXTENSIONS: &[&str] = &["wav", "mp3", "ogg", "flac", "m4a", "aac"];

/// Metadata for one clip. Deliberately cheap: `duration` is `None` until the
/// header has been probed.
#[derive(Debug, Clone)]
pub struct ClipMeta {
    pub path: PathBuf,
    /// File stem, e.g. `airhorn`.
    pub name: String,
    /// Lowercase extension, e.g. `mp3`.
    pub format: String,
    pub bytes: u64,
    /// `None` = not probed yet (or the container doesn't store a frame count).
    pub duration: Option<Duration>,
}

impl ClipMeta {
    /// `horn.mp3` — the file name including extension.
    pub fn file_name(&self) -> String {
        self.path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or(&self.name)
            .to_string()
    }

    /// `1.4 MB`, `212 KB`
    pub fn size_label(&self) -> String {
        const KB: f64 = 1024.0;
        let bytes = self.bytes as f64;
        if bytes >= KB * KB {
            format!("{:.1} MB", bytes / (KB * KB))
        } else {
            format!("{:.0} KB", (bytes / KB).max(1.0))
        }
    }

    /// `0:01.4` — or `—` while unprobed.
    pub fn duration_label(&self) -> String {
        match self.duration {
            Some(d) => format_duration(d),
            None => "—".to_string(),
        }
    }
}

pub fn format_duration(d: Duration) -> String {
    let total = d.as_secs_f64();
    let minutes = (total / 60.0).floor() as u64;
    let seconds = total - (minutes as f64 * 60.0);
    format!("{minutes}:{seconds:04.1}")
}

/// A decoded clip: mono f32 at its native sample rate.
#[derive(Debug, Clone)]
pub struct Clip {
    pub name: String,
    pub samples: Vec<f32>,
    pub sample_rate: u32,
}

impl Clip {
    pub fn duration(&self) -> Duration {
        if self.sample_rate == 0 {
            return Duration::ZERO;
        }
        Duration::from_secs_f64(self.samples.len() as f64 / self.sample_rate as f64)
    }

    /// Linear-interpolation resample to `target` Hz.
    ///
    /// Good enough for one-shot soundboard clips; the voice path stays at the
    /// device rate and never goes through this.
    pub fn resampled(&self, target: u32) -> Vec<f32> {
        if target == 0 || self.sample_rate == 0 || target == self.sample_rate {
            return self.samples.clone();
        }

        let ratio = target as f64 / self.sample_rate as f64;
        let out_len = (self.samples.len() as f64 * ratio).round() as usize;
        let mut out = Vec::with_capacity(out_len);
        for i in 0..out_len {
            let src = i as f64 / ratio;
            let i0 = src.floor() as usize;
            let frac = (src - i0 as f64) as f32;
            let a = self.samples.get(i0).copied().unwrap_or(0.0);
            let b = self.samples.get(i0 + 1).copied().unwrap_or(a);
            out.push(a + (b - a) * frac);
        }
        out
    }
}

/// A directory of clips. Construct with [`Soundboard::scan`].
#[derive(Debug)]
pub struct Soundboard {
    dir: PathBuf,
    clips: Vec<ClipMeta>,
}

impl Soundboard {
    /// **Lazy**: one directory listing, no file contents touched.
    ///
    /// A missing directory yields an empty board rather than an error, so the UI
    /// can show "0 clips" instead of a crash on first run.
    pub fn scan(dir: impl AsRef<Path>) -> Self {
        let dir = dir.as_ref().to_path_buf();
        let mut clips = Vec::new();

        if let Ok(entries) = std::fs::read_dir(&dir) {
            for entry in entries.flatten() {
                let path = entry.path();
                if !path.is_file() {
                    continue;
                }

                let Some(ext) = path.extension().and_then(|e| e.to_str()) else {
                    continue;
                };
                let ext = ext.to_ascii_lowercase();
                if !SUPPORTED_EXTENSIONS.contains(&ext.as_str()) {
                    continue;
                }

                let Some(stem) = path.file_stem().and_then(|s| s.to_str()) else {
                    continue;
                };
                if stem.starts_with('.') {
                    continue;
                }

                let bytes = entry.metadata().map(|m| m.len()).unwrap_or(0);
                let name = stem.to_string();

                clips.push(ClipMeta {
                    path,
                    name,
                    format: ext,
                    bytes,
                    duration: None,
                });
            }
        }

        clips.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Self { dir, clips }
    }

    pub fn dir(&self) -> &Path {
        &self.dir
    }

    pub fn clips(&self) -> &[ClipMeta] {
        &self.clips
    }

    pub fn len(&self) -> usize {
        self.clips.len()
    }

    pub fn is_empty(&self) -> bool {
        self.clips.is_empty()
    }

    pub fn total_bytes(&self) -> u64 {
        self.clips.iter().map(|c| c.bytes).sum()
    }

    /// Look a clip up by index, by exact file name (`horn.mp3`), or by stem
    /// (`horn`). Prefer the file name when the same stem exists in several
    /// formats — see [`Soundboard::same_name`].
    pub fn index_of(&self, query: &str) -> Option<usize> {
        if let Ok(index) = query.parse::<usize>() {
            if index < self.clips.len() {
                return Some(index);
            }
        }

        // exact file name wins ("horn.mp3")
        if let Some(index) = self
            .clips
            .iter()
            .position(|c| c.file_name().eq_ignore_ascii_case(query))
        {
            return Some(index);
        }

        // then the stem ("horn")
        let query = query.to_lowercase();
        self.clips
            .iter()
            .position(|c| c.name.to_lowercase() == query)
    }

    /// Every clip whose stem equals `name` — used to warn about `horn.wav` vs
    /// `horn.mp3` instead of silently picking one.
    pub fn same_name(&self, name: &str) -> Vec<&ClipMeta> {
        let name = name.to_lowercase();
        self.clips
            .iter()
            .filter(|c| c.name.to_lowercase() == name)
            .collect()
    }

    /// **Header-only** duration read. Caches into the metadata.
    pub fn probe(&mut self, index: usize) -> Result<Duration> {
        let path = self
            .clips
            .get(index)
            .with_context(|| format!("clip index {index} out of range"))?
            .path
            .clone();

        let duration = probe_duration(&path)?;
        self.clips[index].duration = Some(duration);
        Ok(duration)
    }

    /// Probe every clip, ignoring the ones whose container has no frame count
    /// (VBR mp3 without a Xing header, for example). Returns how many resolved.
    pub fn probe_all(&mut self) -> usize {
        let mut resolved = 0;
        for index in 0..self.clips.len() {
            if self.clips[index].duration.is_some() {
                resolved += 1;
                continue;
            }
            if self.probe(index).is_ok() {
                resolved += 1;
            }
        }
        resolved
    }

    /// **Full decode.** Only call this when the clip is about to play.
    pub fn load(&self, index: usize) -> Result<Clip> {
        let meta = self
            .clips
            .get(index)
            .with_context(|| format!("clip index {index} out of range"))?;
        decode(&meta.path)
    }
}

/// Read just enough of the container to get a duration.
fn probe_duration(path: &Path) -> Result<Duration> {
    let format = open_format(path)?;
    let track = format
        .default_track()
        .context("no audio track in container")?;

    let params = &track.codec_params;
    let time_base = params.time_base.context("container has no time base")?;
    let n_frames = params
        .n_frames
        .context("container does not store a frame count")?;

    let time = time_base.calc_time(n_frames);
    Ok(Duration::from_secs(time.seconds) + Duration::from_secs_f64(time.frac))
}

fn open_format(path: &Path) -> Result<Box<dyn symphonia::core::formats::FormatReader>> {
    let file = File::open(path).with_context(|| format!("cannot open {}", path.display()))?;
    let stream = MediaSourceStream::new(Box::new(file), Default::default());

    let mut hint = Hint::new();
    if let Some(ext) = path.extension().and_then(|e| e.to_str()) {
        hint.with_extension(ext);
    }

    let probed = symphonia::default::get_probe()
        .format(
            &hint,
            stream,
            &FormatOptions::default(),
            &MetadataOptions::default(),
        )
        .with_context(|| format!("unrecognised audio format: {}", path.display()))?;

    Ok(probed.format)
}

/// Decode a file to mono f32 at its native rate.
pub fn decode(path: &Path) -> Result<Clip> {
    let mut format = open_format(path)?;

    let track = format
        .default_track()
        .context("no audio track in container")?;
    let track_id = track.id;
    let mut sample_rate = track.codec_params.sample_rate.unwrap_or(48_000);

    let mut decoder = symphonia::default::get_codecs()
        .make(&track.codec_params, &DecoderOptions::default())
        .with_context(|| format!("no decoder for {}", path.display()))?;

    let mut samples: Vec<f32> = Vec::new();

    loop {
        let packet = match format.next_packet() {
            Ok(packet) => packet,
            // clean end of stream
            Err(SymphoniaError::IoError(err))
                if err.kind() == std::io::ErrorKind::UnexpectedEof =>
            {
                break
            }
            Err(SymphoniaError::ResetRequired) => break,
            Err(err) => return Err(err.into()),
        };

        if packet.track_id() != track_id {
            continue;
        }

        match decoder.decode(&packet) {
            Ok(audio) => {
                let spec = *audio.spec();
                sample_rate = spec.rate;
                let channels = spec.channels.count().max(1);

                let mut buffer = SampleBuffer::<f32>::new(audio.capacity() as u64, spec);
                buffer.copy_interleaved_ref(audio);

                for frame in buffer.samples().chunks(channels) {
                    samples.push(frame.iter().sum::<f32>() / channels as f32);
                }
            }
            // a corrupt frame shouldn't kill a whole clip
            Err(SymphoniaError::DecodeError(_)) => continue,
            Err(err) => return Err(err.into()),
        }
    }

    let name = path
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or("clip")
        .to_string();

    Ok(Clip {
        name,
        samples,
        sample_rate,
    })
}
