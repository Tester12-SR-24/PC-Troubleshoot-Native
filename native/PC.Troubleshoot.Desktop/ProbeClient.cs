using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PcTroubleshoot.Desktop;

internal sealed class ProbeClient : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Process? process;
    private StreamWriter? input;
    private StreamReader? output;
    private Task<string>? standardError;

    public async Task<JsonDocument> RequestAsync(string action, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await gate.WaitAsync(timeout.Token);
        try
        {
            EnsureStarted();
            await input!.WriteLineAsync(action.AsMemory(), timeout.Token);
            await input.FlushAsync(timeout.Token);
            var line = await output!.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                var detail = standardError is null ? string.Empty : (await standardError).Trim();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                    ? "The system probe stopped without returning a reading."
                    : $"The system probe stopped: {detail}");
            }
            var result = JsonDocument.Parse(line);
            if (result.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.GetString() ?? "The system probe reported an error.";
                result.Dispose();
                throw new InvalidOperationException(message);
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            StopProbe();
            throw new TimeoutException("The system probe did not respond within 15 seconds. Select Refresh to try again.");
        }
        finally { gate.Release(); }
    }

    private void EnsureStarted()
    {
        if (process is { HasExited: false }) return;
        StopProbe();
        var rid = RuntimeInformation.RuntimeIdentifier;
        var root = Environment.GetEnvironmentVariable("PC_TROUBLESHOOT_ROOT") ?? AppContext.BaseDirectory;
        var probe = Path.Combine(root, "runtime", "python", rid, OperatingSystem.IsWindows() ? "probe.exe" : "probe");
        var script = FindRepoFile("python", "probe.py");
        var useBundled = File.Exists(probe);
        var python = Environment.GetEnvironmentVariable("PC_TROUBLESHOOT_PYTHON")
            ?? (OperatingSystem.IsWindows() ? "python" : "python3");
        var start = new ProcessStartInfo(useBundled ? probe : python)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!useBundled)
        {
            if (!File.Exists(script)) throw new FileNotFoundException("The Python probe was not found.", script);
            start.ArgumentList.Add("-u");
            start.ArgumentList.Add(script);
        }
        process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the local system probe.");
        input = process.StandardInput;
        output = process.StandardOutput;
        standardError = process.StandardError.ReadToEndAsync();
    }

    private void StopProbe()
    {
        if (process is { HasExited: false })
        {
            try { process.Kill(true); } catch { }
        }
        process?.Dispose();
        process = null;
        input = null;
        output = null;
        standardError = null;
    }

    private static string FindRepoFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }
        return Path.Combine(AppContext.BaseDirectory, Path.Combine(parts));
    }

    public async ValueTask DisposeAsync()
    {
        if (process is { HasExited: false })
        {
            try { input?.WriteLine("exit"); } catch { }
            process.Kill(true);
            await process.WaitForExitAsync();
        }
        StopProbe();
        gate.Dispose();
    }
}
