using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace PcTroubleshoot.Desktop;

public partial class MainWindow : Window
{
    private readonly ProbeClient probe = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer diagnosticTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly List<double> cpuHistory = [];
    private bool busy;

    public MainWindow()
    {
        InitializeComponent();
        timer.Tick += async (_, _) => await RefreshAsync();
        diagnosticTimer.Tick += (_, _) => ChecksClick(this, new RoutedEventArgs());
        timer.Start();
        diagnosticTimer.Start();
        Opened += async (_, _) => { await RefreshAsync(); ChecksClick(this, new RoutedEventArgs()); };
        Closed += async (_, _) => { timer.Stop(); diagnosticTimer.Stop(); await probe.DisposeAsync(); };
        HistoryCanvas.SizeChanged += (_, _) => DrawHistory();
        ProcessorPageChart.SizeChanged += (_, _) => DrawHistory();
    }

    private async Task RefreshAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            using var reading = await probe.RequestAsync("snapshot");
            RenderSnapshot(reading.RootElement);
            ErrorPanel.IsVisible = false;
            StatusText.Text = "Live";
            StatusDot.Fill = new SolidColorBrush(Color.Parse("#66C397"));
        }
        catch (Exception ex)
        {
            StatusText.Text = "Offline";
            StatusDot.Fill = new SolidColorBrush(Color.Parse("#ED8585"));
            ErrorPanel.IsVisible = true;
            ErrorText.Text = $"Could not read system data: {ex.Message} Check that Python and the psutil package are installed, then select Refresh.";
        }
        finally { busy = false; }
    }

    private void RenderSnapshot(JsonElement data)
    {
        var cpu = Number(data, "cpuPercent");
        var memory = Number(data, "memoryPercent");
        var down = Number(data, "networkDown");
        var up = Number(data, "networkUp");
        var drives = data.TryGetProperty("drives", out var driveArray) ? driveArray.EnumerateArray().ToArray() : [];
        var fullDrive = drives.OrderByDescending(d => Number(d, "percent")).FirstOrDefault();
        var drivePercent = fullDrive.ValueKind == JsonValueKind.Undefined ? 0 : Number(fullDrive, "percent");
        var driveMount = fullDrive.ValueKind == JsonValueKind.Undefined ? "No mounted volumes" : Text(fullDrive, "mount");

        CpuValue.Text = $"{cpu:0}%";
        CpuBar.Value = cpu;
        CpuDetail.Text = $"{Count(data, "cpuCores")} logical processors";
        MemoryValue.Text = $"{memory:0}%";
        MemoryBar.Value = memory;
        MemoryDetail.Text = $"{Bytes(Number(data, "memoryUsed"))} of {Bytes(Number(data, "memoryTotal"))}";
        StorageValue.Text = $"{drives.Length} {(drives.Length == 1 ? "drive" : "drives")}";
        StorageBar.Value = drivePercent;
        StorageDetail.Text = $"{driveMount} · {drivePercent:0}% used";
        NetworkValue.Text = $"↓ {Rate(down)}";
        NetworkDetail.Text = $"↑ {Rate(up)} upload";
        ProcessCount.Text = $"PROCESSES   {Count(data, "processCount"):N0}";
        var uptime = TimeSpan.FromSeconds(Number(data, "uptimeSeconds"));
        UptimeText.Text = $"UPTIME   {(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        SwapText.Text = $"SWAP   {Bytes(Number(data, "swapUsed"))} / {Bytes(Number(data, "swapTotal"))}";
        var timestamp = DateTimeOffset.FromUnixTimeSeconds((long)Number(data, "timestamp"));
        UpdatedText.Text = $"Updated {timestamp.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}";
        ProcessorPageValue.Text = $"{cpu:0}%";
        ProcessorPageDetail.Text = $"{Count(data, "cpuCores")} logical processors are available.";
        MemoryPageValue.Text = $"{memory:0}%";
        MemoryPageBar.Value = memory;
        MemoryPageDetail.Text = $"{Bytes(Number(data, "memoryUsed"))} in use of {Bytes(Number(data, "memoryTotal"))} total.";
        NetworkDownPageValue.Text = Rate(down);
        NetworkUpPageValue.Text = Rate(up);
        ProcessesPageValue.Text = $"{Count(data, "processCount"):N0}";
        StorageRows.Children.Clear();
        if (drives.Length == 0) StorageRows.Children.Add(new TextBlock { Text = "No mounted volumes found.", Foreground = new SolidColorBrush(Color.Parse("#91A0AF")) });
        foreach (var drive in drives)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 2) };
            var detail = new StackPanel { Spacing = 3 };
            detail.Children.Add(new TextBlock { Text = Text(drive, "mount"), FontWeight = FontWeight.SemiBold });
            detail.Children.Add(new TextBlock { Text = Text(drive, "filesystem"), FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#91A0AF")) });
            row.Children.Add(detail);
            row.Children.Add(new TextBlock { Text = $"{Bytes(Number(drive, "free"))} free of {Bytes(Number(drive, "total"))} · {Number(drive, "percent"):0}% used", FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            Grid.SetColumn(row.Children[1], 1);
            StorageRows.Children.Add(row);
        }
        cpuHistory.Add(cpu);
        if (cpuHistory.Count > 60) cpuHistory.RemoveAt(0);
        DrawHistory();
        if (data.TryGetProperty("platformName", out var platform) && platform.GetString() == "Linux")
            Subheading.Text = "Live CPU, memory, storage, and network readings for this Linux device.";
    }

    private void DrawHistory()
    {
        DrawHistoryOn(HistoryCanvas);
        DrawHistoryOn(ProcessorPageChart);
    }

    private void DrawHistoryOn(Canvas canvas)
    {
        canvas.Children.Clear();
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;
        if (width < 10 || height < 10) return;
        for (var row = 1; row <= 3; row++)
        {
            var y = row * height / 4;
            canvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(0, y), EndPoint = new Point(width, y),
                Stroke = new SolidColorBrush(Color.Parse("#263442")), StrokeThickness = 1
            });
        }
        if (cpuHistory.Count < 2) return;
        var points = cpuHistory.Select((value, index) => new Point(index / (double)(cpuHistory.Count - 1) * width,
            height - 6 - Math.Clamp(value, 0, 100) / 100 * (height - 12))).ToArray();
        for (var i = 1; i < points.Length; i++)
        {
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = points[i - 1], EndPoint = points[i], Stroke = new SolidColorBrush(Color.Parse("#66B7F5")), StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round
            };
            canvas.Children.Add(line);
        }
    }

    private static double Number(JsonElement value, string key) => value.TryGetProperty(key, out var property) && property.TryGetDouble(out var number) ? number : 0;
    private static int Count(JsonElement value, string key) => value.TryGetProperty(key, out var property) ? property.ValueKind == JsonValueKind.Array ? property.GetArrayLength() : property.TryGetInt32(out var count) ? count : 0 : 0;
    private static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var property) ? property.GetString() ?? "—" : "—";
    private static string Bytes(double value)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value.ToString(index == 0 ? "0" : "0.0", CultureInfo.InvariantCulture)} {units[index]}";
    }
    private static string Rate(double value) => $"{Bytes(value)}/s";

    private async void RefreshClick(object? sender, RoutedEventArgs e) => await RefreshAsync();
    private async void ChecksClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var result = await probe.RequestAsync("diagnostics");
            var checks = result.RootElement.GetProperty("checks").EnumerateArray().ToArray();
            Alerts.Children.Clear();
            DiagnosticRows.Children.Clear();
            var count = checks.Count(c => Text(c, "level") is "warning" or "bad");
            AlertCount.Text = count == 0 ? "0 OPEN" : $"{count} OPEN";
            foreach (var check in checks)
            {
                DiagnosticRows.Children.Add(CreateCheckRow(check));
                if (Alerts.Children.Count < 3) Alerts.Children.Add(CreateCheckRow(check));
            }
        }
        catch (Exception ex) { ErrorPanel.IsVisible = true; ErrorText.Text = ex.Message; }
    }
    private static Control CreateCheckRow(JsonElement check)
    {
        var level = Text(check, "level");
        var accent = level switch { "warning" => "#E0B25D", "bad" => "#ED8585", _ => "#66C397" };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("12,*"), ColumnSpacing = 9, Margin = new Thickness(0, 2) };
        row.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.Parse(accent)), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Thickness(2, 5, 0, 0) });
        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(new TextBlock { Text = Text(check, "title"), FontWeight = FontWeight.SemiBold, FontSize = 11 });
        text.Children.Add(new TextBlock { Text = Text(check, "detail"), TextWrapping = TextWrapping.Wrap, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#91A0AF")) });
        row.Children.Add(text);
        Grid.SetColumn(text, 1);
        return row;
    }
    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var report = await probe.RequestAsync("report");
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var target = Path.Combine(folder, $"PC-Troubleshoot-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(report.RootElement, new JsonSerializerOptions { WriteIndented = true }));
            ErrorPanel.IsVisible = true;
            ErrorText.Text = $"Report saved to {target}";
        }
        catch (Exception ex) { ErrorText.Text = $"Could not export the report: {ex.Message}"; ErrorPanel.IsVisible = true; }
    }
    private async void UpdateClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var repository = Environment.GetEnvironmentVariable("PC_RELEASE_REPOSITORY");
            var repositoryFile = Path.Combine(AppContext.BaseDirectory, "runtime", "repository.txt");
            if (string.IsNullOrWhiteSpace(repository) && File.Exists(repositoryFile))
                repository = (await File.ReadAllTextAsync(repositoryFile)).Trim();
            repository = (repository ?? string.Empty).Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase).Replace(".git", "", StringComparison.OrdinalIgnoreCase).Trim('/');
            if (!System.Text.RegularExpressions.Regex.IsMatch(repository, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
                throw new InvalidOperationException("This build has no GitHub repository configured for update checks.");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PC-Troubleshoot-Update-Check");
            using var response = await client.GetAsync($"https://api.github.com/repos/{repository}/releases/latest");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GitHub release check failed ({(int)response.StatusCode}).");
            using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = release.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            var latest = Version.TryParse(tag.TrimStart('v'), out var parsedLatest) ? parsedLatest : throw new InvalidOperationException($"Release tag {tag} is not a supported version.");
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            var url = release.RootElement.GetProperty("html_url").GetString() ?? $"https://github.com/{repository}/releases/latest";
            if (latest > current)
            {
                ErrorText.Text = $"Version {latest.Major}.{latest.Minor}.{latest.Build} is available. Opening the GitHub release.";
                ErrorPanel.IsVisible = true;
                OpenExternal(url);
            }
            else
            {
                ErrorText.Text = $"You’re up to date on version {current.Major}.{current.Minor}.{current.Build}.";
                ErrorPanel.IsVisible = true;
            }
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; ErrorPanel.IsVisible = true; }
    }
    private static void OpenExternal(string url)
    {
        if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else Process.Start("xdg-open", url);
    }
    private void Navigate(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
        {
            OverviewPage.IsVisible = page == "Overview";
            ProcessorPage.IsVisible = page == "Processor";
            MemoryPage.IsVisible = page == "Memory";
            StoragePage.IsVisible = page == "Storage";
            NetworkPage.IsVisible = page == "Network";
            ProcessesPage.IsVisible = page == "Processes";
            TroubleshootPage.IsVisible = page == "Troubleshoot";
            Heading.Text = page == "Overview" ? "Good to see you." : page;
            Subheading.Text = page == "Overview" ? "A clear view of what your PC is doing right now." : $"Live system insight for {page.ToLowerInvariant()}.";
            Breadcrumb.Text = page;
            foreach (var button in this.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is string))
                button.Classes.Set("active", string.Equals(button.Tag?.ToString(), page, StringComparison.Ordinal));
            if (page == "Troubleshoot") ChecksClick(this, new RoutedEventArgs());
        }
    }
    private void OpenMonitorClick(object? sender, RoutedEventArgs e)
    {
        var opened = TryStartFirstAvailable(OperatingSystem.IsWindows()
            ? ["taskmgr.exe"] : ["gnome-system-monitor", "ksysguard", "xfce4-taskmanager", "mate-system-monitor"]);
        if (!opened) ShowError("No supported system monitor was found on this desktop.");
    }
    private void OpenSettingsClick(object? sender, RoutedEventArgs e)
    {
        var opened = TryStartFirstAvailable(OperatingSystem.IsWindows()
            ? ["ms-settings:"] : ["gnome-control-center", "systemsettings"]);
        if (!opened) ShowError("No supported system settings app was found on this desktop.");
    }
    private void ShowError(string message) { ErrorText.Text = message; ErrorPanel.IsVisible = true; }
    private static bool TryStartFirstAvailable(IEnumerable<string> commands)
    {
        foreach (var command in commands)
        {
            try { Process.Start(new ProcessStartInfo(command) { UseShellExecute = OperatingSystem.IsWindows() }); return true; }
            catch { }
        }
        return false;
    }
}

