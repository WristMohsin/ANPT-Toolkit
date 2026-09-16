using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI.Views;

public partial class TargetsView : UserControl
{
    private readonly IServiceProvider _services;
    private readonly ICurrentUserService _currentUser;
    private bool _isLoading;
    private System.Windows.Threading.DispatcherTimer? _searchDebounce;

    public TargetsView(IServiceProvider services)
    {
        _services = services;
        _currentUser = services.GetRequiredService<ICurrentUserService>();
        InitializeComponent();
        Loaded += async (_, _) => await LoadTargetsAsync();
        TargetsGrid.SelectionChanged += TargetsGrid_SelectionChanged;
    }

    private void TargetsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = TargetsGrid.SelectedItem is Target;
        EditButton.IsEnabled = hasSelection;
        ArchiveButton.IsEnabled = hasSelection && TargetsGrid.SelectedItem is Target t && t.Status != TargetStatus.Archived;
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Stop();
        _searchDebounce = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounce.Tick += async (_, _) =>
        {
            _searchDebounce.Stop();
            await LoadTargetsAsync();
        };
        _searchDebounce.Start();
    }

    private async void StatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        await LoadTargetsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await LoadTargetsAsync();

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TargetEditWindow(_services, null);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
            await LoadTargetsAsync();
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e) =>
        await EditSelectedAsync();

    private async void TargetsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        await EditSelectedAsync();

    private async Task EditSelectedAsync()
    {
        if (TargetsGrid.SelectedItem is not Target selected)
            return;

        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITargetService>();
        var fresh = await service.GetByIdAsync(selected.Id);
        if (fresh is null)
        {
            MessageBox.Show("Target no longer exists.", "Edit Target", MessageBoxButton.OK, MessageBoxImage.Warning);
            await LoadTargetsAsync();
            return;
        }

        var dialog = new TargetEditWindow(_services, fresh);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
            await LoadTargetsAsync();
    }

    private async void ArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (TargetsGrid.SelectedItem is not Target selected)
            return;

        var confirm = MessageBox.Show(
            $"Archive target \"{selected.Name}\"?\n\nArchived targets remain in the database but are not eligible for future scans.",
            "Archive Target",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITargetService>();
            var result = await service.ArchiveAsync(selected.Id);
            if (!result.Succeeded)
            {
                MessageBox.Show(result.ErrorMessage ?? "Unable to archive target.", "Archive Target",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Log.Information("Target archived from UI: {Name}", selected.Name);
            await LoadTargetsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Archive target UI error");
            MessageBox.Show("Unable to archive target.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadTargetsAsync()
    {
        if (_isLoading)
            return;
        _isLoading = true;

        try
        {
            TargetStatus? statusFilter = null;
            if (StatusFilterCombo.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                !string.IsNullOrEmpty(tag) &&
                Enum.TryParse<TargetStatus>(tag, out var parsed))
            {
                statusFilter = parsed;
            }

            var search = SearchBox.Text?.Trim();

            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITargetService>();
            var list = await service.SearchAsync(search, statusFilter);

            TargetsGrid.ItemsSource = list;
            EmptyStateText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TargetsGrid.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load targets");
            MessageBox.Show("Unable to load targets.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
            TargetsGrid_SelectionChanged(TargetsGrid, new SelectionChangedEventArgs(DataGrid.SelectionChangedEvent, new List<object>(), new List<object>()));
        }
    }
}
