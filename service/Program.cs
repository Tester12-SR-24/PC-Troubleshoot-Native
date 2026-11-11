using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://127.0.0.1:47830");
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSingleton<PythonProbe>();
var app = builder.Build();

app.MapGet("/api/snapshot", async (PythonProbe probe, CancellationToken ct) =>
{
    try { return Results.Ok(await probe.SnapshotAsync(ct)); }
    catch (Exception ex) { return Results.Problem(title: "System probe unavailable", detail: ex.Message, statusCode: 503); }
});

app.MapGet("/api/diagnostics", async (PythonProbe probe, CancellationToken ct) =>
{
    try { return Results.Ok(await probe.DiagnosticsAsync(ct)); }
    catch (Exception ex) { return Results.Problem(title: "Diagnostics unavailable", detail: ex.Message, statusCode: 503); }
});

app.MapPost("/api/tools", (ToolRequest request) =>
{
    var tools = OperatingSystem.IsWindows()
        ? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Task Manager"] = ["taskmgr.exe"], ["Resource Monitor"] = ["resmon.exe"],
            ["Device Manager"] = ["devmgmt.msc"], ["Disk Management"] = ["diskmgmt.msc"],
            ["System Information"] = ["msinfo32.exe"], ["DirectX Diagnostics"] = ["dxdiag.exe"],
            ["Windows Update"] = ["ms-settings:windowsupdate"], ["Display Settings"] = ["ms-settings:display"]
        }
        : new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["System Monitor"] = ["gnome-system-monitor", "ksysguard", "xfce4-taskmanager", "mate-system-monitor"],
            ["Disk Utility"] = ["gnome-disks"], ["System Settings"] = ["gnome-control-center"]
        };
    if (!tools.TryGetValue(request.Name ?? "", out var candidates)) return Results.BadRequest(new { error = "That tool is not on the approved list for this operating system." });
    try
    {
        Exception? lastError = null;
        foreach (var target in candidates)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = OperatingSystem.IsWindows() });
                return Results.Ok(new { message = $"Opened {request.Name}." });
            }
            catch (Exception ex) { lastError = ex; }
        }
        throw lastError ?? new InvalidOperationException("No compatible application was found.");
    }
    catch (Exception ex) { return Results.Problem(title: "Could not open tool", detail: ex.Message, statusCode: 500); }
});

app.MapGet("/api/report", async (PythonProbe probe, CancellationToken ct) =>
{
    try { return Results.Ok(await probe.ReportAsync(ct)); }
    catch (Exception ex) { return Results.Problem(title: "Report unavailable", detail: ex.Message, statusCode: 503); }
});

app.MapPost("/api/shutdown", (IHostApplicationLifetime lifetime) =>
{
    _ = Task.Run(async () => { await Task.Delay(250); lifetime.StopApplication(); });
    return Results.Ok(new { message = "Service shutting down." });
});

app.Run();

record ToolRequest(string? Name);

sealed class PythonProbe : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Process? process;
    private StreamWriter? input;
    private StreamReader? output;
    private readonly string root = Environment.GetEnvironmentVariable("PC_TROUBLESHOOT_ROOT") ?? AppContext.BaseDirectory;

    public Task<JsonElement> SnapshotAsync(CancellationToken ct) => RequestAsync("snapshot", ct);
    public Task<JsonElement> DiagnosticsAsync(CancellationToken ct) => RequestAsync("diagnostics", ct);
    public Task<JsonElement> ReportAsync(CancellationToken ct) => RequestAsync("report", ct);

    private async Task<JsonElement> RequestAsync(string action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            EnsureStarted();
            await input!.WriteLineAsync(action.AsMemory(), ct);
            await input.FlushAsync(ct);
            var line = await output!.ReadLineAsync(ct);
            if (line is null) throw new InvalidOperationException("Python probe exited without a response.");
            using var parsed = JsonDocument.Parse(line);
            if (parsed.RootElement.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
            return parsed.RootElement.Clone();
        }
        finally { gate.Release(); }
    }

    private void EnsureStarted()
    {
        if (process is { HasExited: false }) return;
        var rid = RuntimeInformation.RuntimeIdentifier;
        var packagedProbe = Path.Combine(root, "python", rid, OperatingSystem.IsWindows() ? "probe.exe" : "probe");
        var script = Path.Combine(root, "python", "probe.py");
        var standalone = File.Exists(packagedProbe);
        if (!standalone && !File.Exists(script)) throw new FileNotFoundException("Python probe was not found.", script);
        var python = Environment.GetEnvironmentVariable("PC_TROUBLESHOOT_PYTHON") ?? (OperatingSystem.IsWindows() ? "python" : "python3");
        var start = new ProcessStartInfo(standalone ? packagedProbe : python) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        if (!standalone) { start.ArgumentList.Add("-u"); start.ArgumentList.Add(script); }
        process = Process.Start(start) ?? throw new InvalidOperationException("Python could not be started. Install Python and the packages in python/requirements.txt.");
        input = process.StandardInput;
        output = process.StandardOutput;
        _ = process.StandardError.ReadToEndAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (process is { HasExited: false }) { process.Kill(true); await process.WaitForExitAsync(); }
        process?.Dispose(); gate.Dispose();
    }
}
