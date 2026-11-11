use std::sync::{Arc, Mutex};
use std::time::Duration;

use anyhow::{bail, Context, Result};
use cpal::traits::{DeviceTrait, HostTrait, StreamTrait};
use cpal::{Stream, StreamConfig};
use openmod_core::{ring::Ring, Pipeline, Preset};

fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    match args.first().map(String::as_str) {
        Some("devices") => list_devices(),
        Some("run") => run(&args[1..]),
        Some("presets") => list_presets(),
        _ => {
            eprintln!("openmod — open-source voice changer (foundation test harness)");
            eprintln!();
            eprintln!("usage:");
            eprintln!("  openmod devices          list audio devices");
            eprintln!("  openmod presets          list bundled presets");
            eprintln!("  openmod run [preset]     mic -> effects -> output (default: passthrough)");
            eprintln!("      --vmic               route output to an auto-created virtual mic");
            std::process::exit(2);
        }
    }
}

fn list_devices() -> Result<()> {
    let host = cpal::default_host();
    println!("input devices:");
    for device in host.input_devices().context("no input devices")? {
        let name = device.name().unwrap_or_else(|_| "<unknown>".into());
        println!("  - {name}");
    }
    if let Some(d) = host.default_input_device() {
        println!(
            "  default: {}",
            d.name().unwrap_or_else(|_| "<unknown>".into())
        );
    }
    println!("output devices:");
    for device in host.output_devices().context("no output devices")? {
        let name = device.name().unwrap_or_else(|_| "<unknown>".into());
        println!("  - {name}");
    }
    if let Some(d) = host.default_output_device() {
        println!(
            "  default: {}",
            d.name().unwrap_or_else(|_| "<unknown>".into())
        );
    }
    Ok(())
}

fn list_presets() -> Result<()> {
    let dir = std::path::Path::new("effects");
    if !dir.exists() {
        bail!("no effects/ directory (run from the repo root)");
    }
    for entry in std::fs::read_dir(dir)? {
        let entry = entry?;
        let path = entry.path();
        if path.extension().and_then(|e| e.to_str()) != Some("json") {
            continue;
        }
        match Preset::load(&path) {
            Ok(p) => println!(
                "  {} — {}",
                p.name.unwrap_or_else(|| "<unnamed>".into()),
                path.file_name().unwrap_or_default().to_string_lossy()
            ),
            Err(e) => eprintln!("  {} — INVALID: {e}", path.display()),
        }
    }
    Ok(())
}

fn run(args: &[String]) -> Result<()> {
    // --- flags ----------------------------------------------------------
    let mut vmic = false;
    let mut preset_path: Option<&str> = None;
    for arg in args {
        match arg.as_str() {
            "--vmic" => vmic = true,
            other if other.starts_with('-') => bail!("unknown flag: {other}"),
            other => {
                if preset_path.is_some() {
                    bail!("unexpected argument: {other}");
                }
                preset_path = Some(other);
            }
        }
    }

    // --- pipeline setup -------------------------------------------------
    let pipeline = match preset_path {
        Some(path) => {
            let preset = Preset::load(path)?;
            println!(
                "preset: {}",
                preset.name.as_deref().unwrap_or("<unnamed>")
            );
            preset.to_pipeline()?
        }
        None => {
            println!("preset: passthrough (no effects)");
            Pipeline::new()
        }
    };
    let chain = pipeline.effect_names().join(", ");
    let chain = if chain.is_empty() { "(empty)" } else { &chain };
    println!("chain: [{chain}]");

    // --- device setup ---------------------------------------------------
    let host = cpal::default_host();
    let input = host
        .default_input_device()
        .context("no default input device")?;
    let output = host
        .default_output_device()
        .context("no default output device")?;
    let in_name = input.name().unwrap_or_else(|_| "?".into());
    let out_name = output.name().unwrap_or_else(|_| "?".into());

    let in_config = input.default_input_config()?;
    let out_config = output.default_output_config()?;
    let sample_rate = in_config.sample_rate().0;
    if out_config.sample_rate().0 != sample_rate {
        bail!(
            "input sample rate {} != output {} (foundation-stage: use one device or matching rates)",
            sample_rate,
            out_config.sample_rate().0
        );
    }

    let in_channels = in_config.channels() as usize;
    let out_channels = out_config.channels() as usize;
    let latency_ms = (pipeline.latency_samples() as f32 / sample_rate as f32) * 1000.0;

    println!("input:  {in_name} ({in_channels} ch @ {sample_rate} Hz)");
    println!("output: {out_name} ({out_channels} ch @ {sample_rate} Hz)");
    println!("algorithmic latency: {latency_ms:.1} ms");
    if vmic {
        ensure_vmic()?;
        println!("virtual mic: {VMIC_SINK} ({VMIC_DESC})");
    }
    println!("--- speak into your mic, Ctrl+C to stop ---");

    // --- shared ring ----------------------------------------------------
    let ring = Arc::new(Mutex::new(Ring::new(sample_rate as usize))); // 1s buffer
    let pipeline = Arc::new(Mutex::new(pipeline));

    // --- input stream: mic -> downmix -> pipeline -> ring ---------------
    let in_stream = build_input_stream(
        &input,
        &in_config.into(),
        in_channels,
        sample_rate,
        Arc::clone(&pipeline),
        Arc::clone(&ring),
    )?;

    // --- output stream: ring -> upmix -> speakers -----------------------
    let out_stream = build_output_stream(
        &output,
        &out_config.into(),
        out_channels,
        sample_rate,
        Arc::clone(&ring),
    )?;

    in_stream.play()?;
    out_stream.play()?;

    if vmic {
        route_output_to_vmic()?;
    }

    // --- stats ticker ---------------------------------------------------
    loop {
        std::thread::sleep(Duration::from_secs(2));
        let (len, underruns, overruns) = {
            let r = ring.lock().unwrap();
            (r.len(), r.underruns(), r.overruns())
        };
        let queued_ms = len as f32 / sample_rate as f32 * 1000.0;
        println!(
            "[stats] queued: {queued_ms:.1} ms | underruns: {underruns} | overruns: {overruns}"
        );
        // keep streams alive (dropping them stops audio)
        let _ = (&in_stream, &out_stream);
    }
}

type MonoRing = Arc<Mutex<Ring>>;
type SharedPipeline = Arc<Mutex<Pipeline>>;

fn build_input_stream(
    device: &cpal::Device,
    config: &StreamConfig,
    channels: usize,
    sample_rate: u32,
    pipeline: SharedPipeline,
    ring: MonoRing,
) -> Result<Stream> {
    let stream = device.build_input_stream(
        config,
        move |data: &[f32], _| {
            // downmix interleaved -> mono
            let frames = data.len() / channels.max(1);
            let mut mono = vec![0.0f32; frames];
            for (f, frame) in mono.iter_mut().zip(data.chunks(channels)) {
                *f = frame.iter().sum::<f32>() / channels as f32;
            }
            // process + push
            pipeline.lock().unwrap().process(&mut mono, sample_rate);
            ring.lock().unwrap().push(&mono);
        },
        |err| eprintln!("[input stream error] {err}"),
        None,
    )?;
    Ok(stream)
}

fn build_output_stream(
    device: &cpal::Device,
    config: &StreamConfig,
    channels: usize,
    sample_rate: u32,
    ring: MonoRing,
) -> Result<Stream> {
    let stream = device.build_output_stream(
        config,
        move |data: &mut [f32], _| {
            let frames = data.len() / channels.max(1);
            let mut mono = vec![0.0f32; frames];
            ring.lock().unwrap().pop(&mut mono);
            // upmix mono -> interleaved
            for (frame, &s) in data.chunks_mut(channels).zip(mono.iter()) {
                for sample in frame.iter_mut() {
                    *sample = s;
                }
            }
        },
        |err| eprintln!("[output stream error] {err}"),
        None,
    )?;
    let _ = sample_rate;
    Ok(stream)
}

// --- virtual mic (PulseAudio / PipeWire null sink) -------------------------

const VMIC_SINK: &str = "openmod_vmic";
const VMIC_DESC: &str = "openmod_Virtual_Mic";

fn pactl(args: &[&str]) -> Result<String> {
    let out = std::process::Command::new("pactl")
        .args(args)
        .output()
        .context("pactl not found — install PulseAudio or PipeWire's pulse layer")?;
    if !out.status.success() {
        bail!(
            "pactl {} failed: {}",
            args.join(" "),
            String::from_utf8_lossy(&out.stderr).trim()
        );
    }
    Ok(String::from_utf8_lossy(&out.stdout).into_owned())
}

/// Create the null sink if missing and make sure it is NOT muted.
/// (PipeWire creates null sinks muted on some systems — learned the hard way.)
fn ensure_vmic() -> Result<()> {
    let sinks = pactl(&["list", "short", "sinks"])?;
    let exists = sinks
        .lines()
        .any(|l| l.split_whitespace().nth(1) == Some(VMIC_SINK));
    if !exists {
        println!("creating virtual mic sink '{VMIC_SINK}'...");
        let props = format!("device.description={VMIC_DESC}");
        pactl(&[
            "load-module",
            "module-null-sink",
            "sink_name",
            VMIC_SINK,
            "sink_properties",
            props.as_str(),
        ])?;
    }
    pactl(&["set-sink-mute", VMIC_SINK, "false"])?;
    Ok(())
}

/// Find our output stream's sink-input id in the pulse graph.
fn find_openmod_sink_input() -> Result<Option<String>> {
    let list = pactl(&["list", "sink-inputs"])?;
    for block in list.split("Sink Input #") {
        let id = block.split_whitespace().next().unwrap_or("");
        if !id.chars().all(|c| c.is_ascii_digit()) {
            continue; // preamble chunk before the first entry
        }
        let ours = block.lines().any(|line| {
            let line = line.trim();
            (line.starts_with("application.name")
                || line.starts_with("application.process.binary"))
                && line.contains("openmod")
        });
        if ours {
            return Ok(Some(id.to_string()));
        }
    }
    Ok(None)
}

/// Move our output stream onto the virtual mic. Retries briefly — the stream
/// takes a moment to appear in the pulse graph after play().
fn route_output_to_vmic() -> Result<()> {
    for _ in 0..20 {
        if let Some(id) = find_openmod_sink_input()? {
            pactl(&["move-sink-input", id.as_str(), VMIC_SINK])?;
            println!("output routed -> {VMIC_SINK}");
            println!("  set your mic to \"Monitor of {VMIC_DESC}\" in games/apps");
            return Ok(());
        }
        std::thread::sleep(Duration::from_millis(250));
    }
    bail!("could not locate openmod's output stream for routing (pactl working?)");
}
