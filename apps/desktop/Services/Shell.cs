using System;
using System.Diagnostics;
using System.Text;

namespace OpenMod.Desktop.Services;

/// <summary>
/// Thin wrapper around child processes. Everything the UI does to the outside
/// world (pactl, gdbus, the VB-CABLE installer) goes through here so it's one
/// place to reason about timeouts and never-block-forever behaviour.
/// </summary>
public static class Shell
{
    public sealed record Result(int ExitCode, string StdOut, string StdErr)
    {
        public bool Ok => ExitCode == 0;
        public string Combined => (StdOut + "\n" + StdErr).Trim();
    }

    /// <summary>Run and wait. Never throws: a missing binary is just a failure.</summary>
    public static Result Run(string file, params string[] args) =>
        Run(file, TimeSpan.FromSeconds(10), args);

    public static Result Run(string file, TimeSpan timeout, params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            using var process = Process.Start(info);
            if (process is null)
            {
                return new Result(-1, "", $"couldn't start {file}");
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // already gone
                }

                return new Result(-1, stdout.ToString(), $"{file} timed out");
            }

            process.WaitForExit(); // flush async readers
            return new Result(process.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch (Exception ex)
        {
            return new Result(-1, "", ex.Message);
        }
    }

    public static bool Exists(string file)
    {
        try
        {
            var result = Run("/usr/bin/env", TimeSpan.FromSeconds(5), "sh", "-c", $"command -v {file}");
            return result.Ok && !string.IsNullOrWhiteSpace(result.StdOut);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Open a URL in the user's browser. Failure is not an error worth showing.</summary>
    public static bool OpenUrl(string url)
    {
        try
        {
            var info = new ProcessStartInfo(url) { UseShellExecute = true };
            Process.Start(info);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
