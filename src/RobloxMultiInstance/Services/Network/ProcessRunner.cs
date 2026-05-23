using System.Diagnostics;
using System.IO;

namespace RobloxMultiInstance.Services.Network;

public static class ProcessRunner
{
    public record Result(int ExitCode, string Stdout, string Stderr);

    public static async Task<Result> RunAsync(
        string exe,
        string args,
        CancellationToken ct = default,
        TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        Process p;
        try
        {
            p = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
        }
        catch (Exception ex)
        {
            return new Result(-1, string.Empty, $"Failed to start: {ex.Message}");
        }

        using (p)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout.HasValue) linked.CancelAfter(timeout.Value);

            var stdoutTask = p.StandardOutput.ReadToEndAsync(linked.Token);
            var stderrTask = p.StandardError.ReadToEndAsync(linked.Token);

            try
            {
                await p.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return new Result(-1, string.Empty, "Timed out or cancelled");
            }

            string stdout, stderr;
            try { stdout = await stdoutTask; } catch { stdout = string.Empty; }
            try { stderr = await stderrTask; } catch { stderr = string.Empty; }
            return new Result(p.ExitCode, stdout, stderr);
        }
    }

    public static string? FindOnPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var candidate = Path.Combine(dir, exeName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }
}
