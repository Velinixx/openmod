using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMod.Desktop.Services;

/// <summary>
/// The optional Windows add-on: VB-CABLE, installed by us, on request only.
///
/// Rules this file exists to keep:
///  - never bundle or repackage somebody else's driver — we fetch the official
///    zip from vb-audio.com every time, so we always get their current build
///  - never hardcode a version number: VB-Audio bumped Pack43 → Pack46 in a
///    single page edit, and a pinned URL would have rotted silently
///  - never install silently or behind the user's back: we launch their own
///    setup exe, elevated, with the normal UI, and they click through it
///  - VB-Audio documents no silent flags, so we don't invent any
///
/// The page and zip handling is platform-agnostic on purpose (it's unit
/// testable on Linux); only the final elevated launch is Windows-only.
/// </summary>
public static class CableInstall
{
    public const string PageUrl = "https://vb-audio.com/Cable/";
    public const string DownloadPageUrl = "https://vb-audio.com/Cable/index.htm";

    /// <summary>Matches the current package link on their page, capturing the pack number.</summary>
    private static readonly Regex PackageLink = new(
        @"https://download\.vb-audio\.com/Download_CABLE/VBCABLE_Driver_Pack(\d+)\.zip",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(2),
    };

    static CableInstall()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("openmod/0.1 (first-run setup; +https://github.com/Velinixx/openmod)");
    }

    /// <summary>
    /// Pull the newest VB-CABLE package URL out of their download page.
    /// Pure and side-effect free so it can be tested without a network.
    /// </summary>
    public static string? FindInstallerUrl(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        return PackageLink.Matches(html)
            .Select(m => (Url: m.Value, Pack: int.Parse(m.Groups[1].Value)))
            .OrderByDescending(m => m.Pack)
            .Select(m => m.Url)
            .FirstOrDefault();
    }

    public sealed record Outcome(bool Ok, string Message, bool RebootNeeded = false);

    /// <summary>
    /// Download, extract and hand their installer to the OS. Reports progress as
    /// human sentences because they land in the wizard's status line.
    /// </summary>
    public static async Task<Outcome> InstallAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellation = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new Outcome(false,
                "the one-press cable install is a Windows thing — Linux needs nothing installed");
        }

        string html;
        try
        {
            progress?.Report("asking vb-audio.com for their current package…");
            html = await Http.GetStringAsync(PageUrl, cancellation);
        }
        catch (Exception ex)
        {
            return new Outcome(false,
                $"couldn't reach vb-audio.com ({ex.Message}) — use \"open vb-audio.com\" instead");
        }

        var url = FindInstallerUrl(html);
        if (url is null)
        {
            return new Outcome(false,
                "vb-audio.com didn't list a VB-CABLE package we recognise — use \"open vb-audio.com\" instead");
        }

        var work = Path.Combine(Path.GetTempPath(), "openmod-vbcable-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(work);
            var zip = Path.Combine(work, "vbcable.zip");

            progress?.Report("downloading the official installer…");
            await using (var source = await Http.GetStreamAsync(url, cancellation))
            await using (var target = File.Create(zip))
            {
                await source.CopyToAsync(target, cancellation);
            }

            progress?.Report("extracting…");
            var extracted = Path.Combine(work, "unpacked");
            ZipFile.ExtractToDirectory(zip, extracted);

            var setup = FindSetup(extracted);
            if (setup is null)
            {
                return new Outcome(false,
                    "the package didn't contain a setup program we recognise — use \"open vb-audio.com\" instead");
            }

            progress?.Report("starting their installer — approve the admin prompt…");

            // Verb=runas is the only elevation API that exists everywhere on
            // Windows; it shows the real UAC dialog, which is the honest thing.
            var start = new System.Diagnostics.ProcessStartInfo(setup)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(setup) ?? extracted,
            };

            using var process = System.Diagnostics.Process.Start(start);
            if (process is null)
            {
                return new Outcome(false, "couldn't start their installer — use \"open vb-audio.com\" instead");
            }

            await process.WaitForExitAsync(cancellation);

            return process.ExitCode == 0
                ? new Outcome(true,
                    "VB-CABLE installed — reboot, then openmod will find it as your virtual mic", true)
                : new Outcome(false,
                    $"their installer exited with code {process.ExitCode} — you may have cancelled it");
        }
        catch (OperationCanceledException)
        {
            return new Outcome(false, "cancelled");
        }
        catch (Exception ex)
        {
            return new Outcome(false, $"the install didn't finish: {ex.Message}");
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (Exception)
            {
                // temp leftovers are fine
            }
        }
    }

    /// <summary>
    /// Their zip ships both architectures; prefer the x64 setup when present.
    /// Matched by name, not by a hardcoded filename, so a rename doesn't break us.
    /// </summary>
    public static string? FindSetup(string directory)
    {
        try
        {
            var candidates = Directory.EnumerateFiles(directory, "*Setup*.exe", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(directory, "*setup*.exe", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return candidates.FirstOrDefault(p => p.Contains("x64", StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault(p => !p.Contains("x86", StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
