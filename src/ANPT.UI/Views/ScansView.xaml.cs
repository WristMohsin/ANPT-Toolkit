using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI.Views;

public partial class ScansView : UserControl
{
    private readonly IServiceProvider _services;
    private bool _isLoading;
    private bool _isCancelling;
    private bool _isStarting;
    private System.Windows.Threading.DispatcherTimer? _searchDebounce;
    private int _loadGeneration;

    public ScansView(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        Loaded += async (_, _) => await LoadScansAsync();
        Unloaded += (_, _) =>
        {
            _searchDebounce?.Stop();
            _searchDebounce = null;
        };
        ScansGrid.SelectionChanged += ScansGrid_SelectionChanged;
    }

    private void ScansGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ScansGrid.SelectedItem as Scan;
        DetailsButton.IsEnabled = selected is not null;
        StartScanButton.IsEnabled = selected is not null &&
            !_isStarting &&
            selected.Status == ScanStatus.Queued;
        AnalyzeScanButton.IsEnabled = selected is not null &&
            selected.Status == ScanStatus.Completed;
        CancelScanButton.IsEnabled = selected is not null &&
            !_isCancelling &&
            (selected.Status == ScanStatus.Queued || selected.Status == ScanStatus.Running);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_searchDebounce is null)
        {
            _searchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounce.Tick += async (_, _) =>
            {
                _searchDebounce?.Stop();
                await LoadScansAsync();
            };
        }
        else
        {
            _searchDebounce.Stop();
        }
        _searchDebounce.Start();
    }

    private async void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadScansAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await LoadScansAsync();

    private async void NewScanButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ScanCreateWindow(_services) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
            await LoadScansAsync();
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e) => OpenDetails();

    private void ScansGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenDetails();

    private void OpenDetails()
    {
        if (ScansGrid.SelectedItem is not Scan selected) return;
        var window = new ScanDetailsWindow(selected) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private async void StartScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isStarting) return;
        if (ScansGrid.SelectedItem is not Scan selected) return;
        if (selected.Status != ScanStatus.Queued) return;

        _isStarting = true;
        StartScanButton.IsEnabled = false;
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScanService>();
            var result = await service.StartAsync(selected.Id);
            if (!result.Succeeded)
            {
                MessageBox.Show(result.ErrorMessage ?? "Unable to start scan.", "Start Scan",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Log.Information("Scan started from UI: {Id}", selected.Id);
            await LoadScansAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Start scan UI error");
            MessageBox.Show("Unable to start scan.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isStarting = false;
            ScansGrid_SelectionChanged(ScansGrid, new SelectionChangedEventArgs(
                DataGrid.SelectionChangedEvent, new List<object>(), new List<object>()));
        }
    }

    private async void AnalyzeScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScansGrid.SelectedItem is not Scan selected) return;
        if (selected.Status != ScanStatus.Completed)
        {
            MessageBox.Show("Only completed scans can be analyzed.", "Analyze Scan",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AnalyzeScanButton.IsEnabled = false;
        try
        {
            using var scope = _services.CreateScope();
            var analysis = scope.ServiceProvider.GetRequiredService<ISecurityAnalysisService>();
            var result = await analysis.AnalyzeScanAsync(selected.Id);
            if (!result.Succeeded)
            {
                MessageBox.Show(result.ErrorMessage ?? "Analysis failed.", "Analyze Scan",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                $"Analysis complete.\n\nRules executed: {result.RulesExecuted}\nFindings created: {result.FindingsCreated}\nPrior analysis findings replaced: {result.FindingsRemoved}\nDuration: {result.Duration.TotalSeconds:0.00}s",
                "Analyze Scan",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Analyze scan UI error");
            MessageBox.Show("Unexpected error during analysis.", "Analyze Scan",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScansGrid_SelectionChanged(ScansGrid, new SelectionChangedEventArgs(
                DataGrid.SelectionChangedEvent, new List<object>(), new List<object>()));
        }
    }

    private async void CancelScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCancelling) return;
        if (ScansGrid.SelectedItem is not Scan selected) return;

        var confirm = MessageBox.Show(
            $"Cancel scan '{selected.Name}'?\n\nIf the scan is Running, the Nmap process will be terminated.",
            "Cancel Scan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        _isCancelling = true;
        CancelScanButton.IsEnabled = false;
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScanService>();
            var result = await service.CancelAsync(selected.Id);
            if (!result.Succeeded)
            {
                MessageBox.Show(result.ErrorMessage ?? "Unable to cancel scan.", "Cancel Scan",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Log.Information("Scan cancelled from UI: {Id}", selected.Id);
            await LoadScansAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cancel scan UI error");
            MessageBox.Show("Unable to cancel scan.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isCancelling = false;
            ScansGrid_SelectionChanged(ScansGrid, new SelectionChangedEventArgs(
                DataGrid.SelectionChangedEvent, new List<object>(), new List<object>()));
        }
    }

    private async Task LoadScansAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        var generation = ++_loadGeneration;
        try
        {
            ScanStatus? statusFilter = null;
            if (StatusFilterCombo.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                !string.IsNullOrEmpty(tag) &&
                Enum.TryParse<ScanStatus>(tag, out var parsed))
            {
                statusFilter = parsed;
            }

            var search = SearchBox.Text?.Trim();
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScanService>();
            var list = await service.SearchAsync(search, statusFilter, null, null);

            if (generation != _loadGeneration) return;

            ScansGrid.ItemsSource = list;
            EmptyStateText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ScansGrid.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load scans");
            if (generation == _loadGeneration)
            {
                MessageBox.Show("Unable to load scans. Please try again.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _isLoading = false;
                ScansGrid_SelectionChanged(ScansGrid, new SelectionChangedEventArgs(
                    DataGrid.SelectionChangedEvent, new List<object>(), new List<object>()));
            }
        }
    }
}
