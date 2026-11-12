using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;

namespace OpenMod.Desktop.Services;

/// <summary>
/// Reads the desktop accent colour from the XDG settings portal
/// (<c>org.freedesktop.appearance accent-color</c>), which is how KDE, GNOME and
/// most Wayland compositors expose it.
///
/// This shells out to <c>gdbus</c> instead of pulling in a D-Bus library: it's
/// one call at startup, and if gdbus/portal isn't there we degrade to the user's
/// chosen accent instead of failing.
/// </summary>
public static partial class SystemAccent
{
    [GeneratedRegex(@"\d+\.\d+|\d+")]
    private static partial Regex NumberRegex();

    /// <summary>
    /// Portal returns linear-ish sRGB triples as doubles. We return a pair:
    /// a lightened tone for dark themes and the raw colour for light themes.
    /// </summary>
    public static bool TryRead(out Color bright, out Color deep)
    {
        bright = default;
        deep = default;

        try
        {
            var psi = new ProcessStartInfo("gdbus")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("call");
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add("--dest");
            psi.ArgumentList.Add("org.freedesktop.portal.Desktop");
            psi.ArgumentList.Add("--object-path");
            psi.ArgumentList.Add("/org/freedesktop/portal/desktop");
            psi.ArgumentList.Add("--method");
            psi.ArgumentList.Add("org.freedesktop.portal.Settings.Read");
            psi.ArgumentList.Add("org.freedesktop.appearance");
            psi.ArgumentList.Add("accent-color");

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(1500))
            {
                try
                {
                    process.Kill();
                }
                catch (Exception)
                {
                    // already gone
                }

                return false;
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            // e.g. (<<(0.21176470588235294, 0.51764705882352946, 0.8901960784313725)>>,)
            var numbers = NumberRegex().Matches(output);
            if (numbers.Count < 3)
            {
                return false;
            }

            var r = ToByte(numbers[0].Value);
            var g = ToByte(numbers[1].Value);
            var b = ToByte(numbers[2].Value);

            deep = Color.FromRgb(r, g, b);
            bright = Lighten(deep, 0.32);
            return true;
        }
        catch (Exception)
        {
            // no gdbus, no session bus, no portal — all fine
            return false;
        }
    }

    private static byte ToByte(string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        // portal can hand back either 0..1 or 0..255 depending on backend
        var scaled = value <= 1.0 ? value * 255.0 : value;
        return (byte)Math.Clamp(Math.Round(scaled), 0, 255);
    }

    /// <summary>Blend toward white — keeps the hue, lifts it for dark backgrounds.</summary>
    public static Color Lighten(Color color, double amount)
    {
        var a = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Round(color.R + (255 - color.R) * a),
            (byte)Math.Round(color.G + (255 - color.G) * a),
            (byte)Math.Round(color.B + (255 - color.B) * a));
    }

    /// <summary>Blend toward black — used to derive the "deep" tone from a custom colour.</summary>
    public static Color Darken(Color color, double amount)
    {
        var a = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Round(color.R * (1 - a)),
            (byte)Math.Round(color.G * (1 - a)),
            (byte)Math.Round(color.B * (1 - a)));
    }
}
