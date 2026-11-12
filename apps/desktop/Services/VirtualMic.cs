using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenMod.Desktop.Services;

public enum VirtualMicState
{
    /// <summary>Present and usable — pick it as your mic.</summary>
    Ready,

    /// <summary>We know how to make one, it just isn't there yet.</summary>
    Missing,

    /// <summary>This platform needs a real driver (Windows) or has no support we can offer.</summary>
    NeedsDriver,
}

public sealed record VirtualMicStatus(
    VirtualMicState State,
    string SinkName,
    string MonitorName,
    string Detail,
    string? CableName = null)
{
    public bool IsReady => State == VirtualMicState.Ready;

    public string Label => State switch
    {
        VirtualMicState.Ready => "ready",
        VirtualMicState.Missing => "not created yet",
        _ => "not detected",
    };
}

/// <summary>
/// The fake microphone. On Linux this is a PipeWire null sink — free, no driver,
/// no elevation, no reboot — which is the entire reason openmod is Linux-first.
/// On Windows it's a kernel driver somebody else has to install, so all we do is
/// detect one.
/// </summary>
public static class VirtualMic
{
    public const string SinkName = "openmod_vmic";
    public const string Description = "openmod_Virtual_Mic";
    public static string MonitorName => SinkName + ".monitor";

    /// <summary>Names that mean "somebody already installed a virtual cable".</summary>
    private static readonly string[] CableHints =
    [
        "CABLE Output", "CABLE Input", "VB-Audio", "VoiceMeeter", "Voicemod",
        "Virtual Audio", "BlackHole", "Loopback",
    ];

    public static VirtualMicStatus Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return DetectWindows();
        }

        if (!OperatingSystem.IsLinux())
        {
            return new VirtualMicStatus(VirtualMicState.NeedsDriver, SinkName, MonitorName,
                "this platform needs a virtual audio device from somewhere else");
        }

        if (!Shell.Exists("pactl"))
        {
            return new VirtualMicStatus(VirtualMicState.NeedsDriver, SinkName, MonitorName,
                "no pactl on PATH — openmod needs PipeWire or PulseAudio");
        }

        var sinks = Shell.Run("pactl", "list", "short", "sinks");
        if (!sinks.Ok)
        {
            return new VirtualMicStatus(VirtualMicState.NeedsDriver, SinkName, MonitorName,
                "couldn't talk to the audio server");
        }

        var present = sinks.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t'))
            .Any(parts => parts.Length >= 2 && parts[1].Trim() == SinkName);

        return present
            ? new VirtualMicStatus(VirtualMicState.Ready, SinkName, MonitorName,
                "PipeWire null sink — created for you, already running")
            : new VirtualMicStatus(VirtualMicState.Missing, SinkName, MonitorName,
                "PipeWire null sink — one command away, nothing to download");
    }

    /// <summary>
    /// Create the null sink if it's missing (and always unmute it — PipeWire
    /// creates null sinks muted, which silently breaks the whole thing).
    /// Linux only; a no-op elsewhere.
    /// </summary>
    public static bool Ensure(out string detail)
    {
        if (!OperatingSystem.IsLinux())
        {
            detail = "on this platform a virtual device has to be installed separately";
            return false;
        }

        var current = Detect();
        if (!current.IsReady)
        {
            var load = Shell.Run(
                "pactl", "load-module", "module-null-sink",
                $"sink_name={SinkName}",
                $"sink_properties=device.description={Description}");

            if (!load.Ok)
            {
                detail = load.Combined.Length > 0 ? load.Combined : "pactl load-module failed";
                return false;
            }
        }

        var unmute = Shell.Run("pactl", "set-sink-mute", SinkName, "0");
        if (!unmute.Ok)
        {
            detail = "created, but couldn't unmute it — check pavucontrol";
            return false;
        }

        detail = current.IsReady ? "openmod_vmic is unmuted and ready" : "openmod_vmic created and unmuted";
        return true;
    }

    /// <summary>
    /// Play a short tone into the sink so people can prove the routing works
    /// before they blame openmod. Returns false if no player is available —
    /// that's a "couldn't test", not a "broken".
    /// </summary>
    public static bool PlayTestTone(out string detail)
    {
        if (!OperatingSystem.IsLinux())
        {
            detail = "tone test is Linux-only for now";
            return false;
        }

        var wav = Path.Combine(Path.GetTempPath(), "openmod-vmic-test.wav");
        try
        {
            File.WriteAllBytes(wav, ToneWav(seconds: 0.6, frequency: 440, sampleRate: 44100));
        }
        catch (Exception ex)
        {
            detail = "couldn't write a test tone: " + ex.Message;
            return false;
        }

        // pw-play is the native path; paplay also works through the Pulse layer
        var player = Shell.Exists("pw-play") ? "pw-play" : Shell.Exists("paplay") ? "paplay" : null;
        if (player is null)
        {
            detail = "no pw-play or paplay on PATH — can't play a test tone";
            return false;
        }

        var result = player == "pw-play"
            ? Shell.Run(player, "--target", SinkName, wav)
            : Shell.Run(player, $"--device={SinkName}", wav);

        detail = result.Ok
            ? "played a 440 Hz tone into openmod_vmic — capture openmod_vmic.monitor to hear it"
            : "the tone didn't play: " + result.Combined;
        return result.Ok;
    }

    private static VirtualMicStatus DetectWindows()
    {
        // No driver API here — ask the OS for sound devices and match names.
        // Read-only, no elevation.
        var query = "Get-CimInstance Win32_SoundDevice | Select-Object -ExpandProperty Name";
        var result = Shell.Run("powershell", "-NoProfile", "-NonInteractive", "-Command", query);

        if (result.Ok)
        {
            var names = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .ToList();

            var cable = names.FirstOrDefault(n =>
                CableHints.Any(hint => n.Contains(hint, StringComparison.OrdinalIgnoreCase)));

            if (cable is not null)
            {
                return new VirtualMicStatus(VirtualMicState.Ready, "CABLE Output", "CABLE Input",
                    $"found {cable} — pick \"{cable}\" as your microphone", cable);
            }
        }

        return new VirtualMicStatus(VirtualMicState.NeedsDriver, "CABLE Output", "CABLE Input",
            "no virtual audio device found on this machine");
    }

    /// <summary>Minimal 16-bit mono PCM wav — no audio library needed for a beep.</summary>
    private static byte[] ToneWav(double seconds, double frequency, int sampleRate)
    {
        var frames = (int)(seconds * sampleRate);
        var data = new byte[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            // fade in/out so it doesn't click
            var t = (double)i / frames;
            var envelope = Math.Min(1.0, Math.Min(t / 0.02, (1 - t) / 0.05));
            var sample = Math.Sin(2 * Math.PI * frequency * i / sampleRate) * 0.25 * envelope;
            var value = (short)(sample * short.MaxValue);
            data[i * 2] = (byte)(value & 0xFF);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + data.Length);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);              // chunk size
        writer.Write((short)1);        // PCM
        writer.Write((short)1);        // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);  // byte rate
        writer.Write((short)2);        // block align
        writer.Write((short)16);       // bits per sample
        writer.Write("data"u8.ToArray());
        writer.Write(data.Length);
        writer.Write(data);

        writer.Flush();
        return stream.ToArray();
    }
}
