using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using QuantumOverdrive.Models;
using QuantumOverdrive.Services;
using Path = System.IO.Path;

namespace QuantumOverdrive;

public partial class MainWindow : Window
{
    private readonly HardwareMonitor _monitor = new();
    private readonly ObservableCollection<GameEntry> _games = [];
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _gpuHistory = new();
    private readonly System.Windows.Threading.DispatcherTimer _timer = new();
    private DateTime _lastClockUpdate = DateTime.MinValue;
    private bool _isRefreshing;
    private bool _isReadingOptionalMetrics;

    public MainWindow()
    {
        InitializeComponent();
        GameList.ItemsSource = _games;
        IntervalCombo.SelectionChanged += IntervalCombo_SelectionChanged;
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        await RefreshMetricsAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _monitor.Dispose();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isRefreshing)
            return;
        await RefreshMetricsAsync();
    }

    private async Task RefreshMetricsAsync()
    {
        _isRefreshing = true;
        try
        {
            var metrics = await _monitor.ReadCoreAsync();
            CpuUsageText.Text = metrics.CpuUsage?.ToString("0") ?? "--";
            MemoryUsageText.Text = metrics.MemoryUsagePercent?.ToString("0") ?? "--";
            MemoryDetailText.Text = metrics.MemoryUsedGb is double used && metrics.MemoryTotalGb is double total
                ? $"{used:0.0} / {total:0.0} GB used"
                : "Memory details unavailable";
            CpuNameText.Text = metrics.CpuUsage.HasValue ? "Total processor utilization" : "Usage unavailable";
            MemoryProgress.Value = metrics.MemoryUsagePercent ?? 0;
            MemoryBarLabel.Text = metrics.MemoryUsagePercent is double percent && metrics.MemoryUsedGb is double memoryUsed && metrics.MemoryTotalGb is double memoryTotal
                ? $"{memoryUsed:0.0} GB of {memoryTotal:0.0} GB · {percent:0}%"
                : "Memory usage unavailable";
            LastUpdatedText.Text = $"Updated {DateTime.Now:HH:mm:ss}";
            if (DateTime.Now - _lastClockUpdate >= TimeSpan.FromSeconds(1))
            {
                ClockText.Text = DateTime.Now.ToString("ddd, MMM d  ·  HH:mm:ss");
                _lastClockUpdate = DateTime.Now;
            }

            AddHistory(_cpuHistory, metrics.CpuUsage);
            DrawCharts();
            StatusText.Text = "Monitoring active";

            if (!_isReadingOptionalMetrics)
                _ = RefreshOptionalMetricsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Telemetry error";
            NotificationText.Text = $"Could not refresh hardware telemetry: {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RefreshOptionalMetricsAsync()
    {
        _isReadingOptionalMetrics = true;
        try
        {
            var metrics = await _monitor.ReadOptionalAsync();
            GpuUsageText.Text = metrics.GpuUsage?.ToString("0") ?? "--";
            GpuNameText.Text = metrics.GpuUsage.HasValue
                ? "GPU core utilization"
                : "No GPU load sensor detected";
            CpuTemperatureText.Text = metrics.CpuTemperatureC?.ToString("0") ?? "--";
            TemperatureSourceText.Text = metrics.CpuTemperatureC.HasValue
                ? "Hardware temperature sensor"
                : "No CPU temperature sensor detected";
            AddHistory(_gpuHistory, metrics.GpuUsage);
            DrawCharts();
            if (!metrics.GpuUsage.HasValue || !metrics.CpuTemperatureC.HasValue)
                NotificationText.Text = "GPU/CPU sensors not exposed by this hardware or driver. Try launching Quantum Overdrive as administrator.";
        }
        catch (Exception ex)
        {
            GpuUsageText.Text = "--";
            GpuNameText.Text = "GPU counters unavailable";
            CpuTemperatureText.Text = "--";
            TemperatureSourceText.Text = "Sensor read failed";
            NotificationText.Text = $"Optional GPU/temperature sensor read failed: {ex.Message}";
        }
        finally
        {
            _isReadingOptionalMetrics = false;
        }
    }

    private static void AddHistory(Queue<double> history, double? value)
    {
        history.Enqueue(value ?? 0);
        while (history.Count > 60)
            history.Dequeue();
    }

    private void DrawCharts()
    {
        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        SetChartPoints(CpuChartLine, _cpuHistory, width, height);
        SetChartPoints(GpuChartLine, _gpuHistory, width, height);
    }

    private static void SetChartPoints(Polyline line, IEnumerable<double> values, double width, double height)
    {
        var points = values.ToArray();
        line.Points = new PointCollection(points.Select((value, index) =>
        {
            var x = points.Length <= 1 ? width : index * width / 59d;
            var y = height - Math.Clamp(value, 0, 100) / 100d * height;
            return new Point(x, y);
        }));
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawCharts();

    private void ShowPage(string page)
    {
        DashboardPage.Visibility = page == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        BoosterPage.Visibility = page == "booster" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = page == "library" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch
        {
            "booster" => "Game booster",
            "library" => "Game library",
            "settings" => "Settings",
            _ => "Performance overview"
        };
        PageSubtitle.Text = page switch
        {
            "booster" => "Prepare Windows and launch your games with fewer distractions.",
            "library" => "Your shortcuts to the games you play.",
            "settings" => "Configure local hardware monitoring.",
            _ => "Your hardware, at a glance. Updates automatically."
        };

        DashboardNav.Background = page == "dashboard" ? new SolidColorBrush(Color.FromRgb(37, 32, 59)) : (Brush)FindResource("CardBrush");
        BoosterNav.Background = page == "booster" ? new SolidColorBrush(Color.FromRgb(37, 32, 59)) : (Brush)FindResource("CardBrush");
        LibraryNav.Background = page == "library" ? new SolidColorBrush(Color.FromRgb(37, 32, 59)) : (Brush)FindResource("CardBrush");
        SettingsNav.Background = page == "settings" ? new SolidColorBrush(Color.FromRgb(37, 32, 59)) : (Brush)FindResource("CardBrush");
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowPage("dashboard");
    private void BoosterNav_Click(object sender, RoutedEventArgs e) => ShowPage("booster");
    private void LibraryNav_Click(object sender, RoutedEventArgs e) => ShowPage("library");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage("settings");

    private void IntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IntervalCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var seconds))
        {
            _timer.Interval = TimeSpan.FromSeconds(seconds);
            NotificationText.Text = $"Monitoring interval set to {seconds} second{(seconds == 1 ? "" : "s")}.";
        }
    }

    private void BrowseGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a game executable",
            Filter = "Application files (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            GamePathBox.Text = dialog.FileName;
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add a game to your library",
            Filter = "Application files (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;
        AddGame(dialog.FileName);
    }

    private void AddGame(string path)
    {
        if (_games.Any(game => string.Equals(game.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            NotificationText.Text = "That game is already in your library.";
            return;
        }
        _games.Add(new GameEntry(Path.GetFileNameWithoutExtension(path), path));
        NotificationText.Text = $"Added {Path.GetFileNameWithoutExtension(path)} to your game library.";
        GamePathBox.Text = path;
    }

    private void LaunchLibraryGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            LaunchGame(path);
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e) => LaunchGame(GamePathBox.Text);

    private void LaunchGame(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            NotificationText.Text = "Select an existing game executable before launching.";
            return;
        }
        try
        {
            var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            if (process is null)
            {
                NotificationText.Text = "Windows did not return a process for the game.";
                return;
            }
            if (HighPriorityCheck.IsChecked == true)
            {
                try
                {
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                    NotificationText.Text = $"Launched {Path.GetFileNameWithoutExtension(path)} with Above normal priority.";
                }
                catch (InvalidOperationException)
                {
                    NotificationText.Text = $"Launched {Path.GetFileNameWithoutExtension(path)}; Windows did not allow changing its priority.";
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    NotificationText.Text = $"Launched game, but priority could not be changed: {ex.Message}";
                }
            }
            else
                NotificationText.Text = $"Launched {Path.GetFileNameWithoutExtension(path)}.";

            if (!_games.Any(game => string.Equals(game.Path, path, StringComparison.OrdinalIgnoreCase)))
                AddGame(path);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            NotificationText.Text = $"Could not launch game: {ex.Message}";
        }
    }

    private async void ApplyGamingSetup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SetPowerPlanAsync("SCHEME_MIN");
            Process.Start(new ProcessStartInfo("ms-settings:gaming-gamemode") { UseShellExecute = true });
            NotificationText.Text = "High performance plan applied. Review and enable Game Mode in Windows Settings.";
        }
        catch (Exception ex)
        {
            NotificationText.Text = $"Gaming setup could not be fully applied: {ex.Message}";
        }
    }

    private async void RestoreBalanced_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SetPowerPlanAsync("SCHEME_BALANCED");
            NotificationText.Text = "Windows Balanced power plan restored.";
        }
        catch (Exception ex)
        {
            NotificationText.Text = $"Could not restore Balanced plan: {ex.Message}";
        }
    }

    private void OpenGameMode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:gaming-gamemode") { UseShellExecute = true });
            NotificationText.Text = "Opened Windows Game Mode settings.";
        }
        catch (Exception ex)
        {
            NotificationText.Text = $"Could not open Game Mode settings: {ex.Message}";
        }
    }

    private static async Task SetPowerPlanAsync(string plan)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powercfg.exe", $"/setactive {plan}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
    }
}
