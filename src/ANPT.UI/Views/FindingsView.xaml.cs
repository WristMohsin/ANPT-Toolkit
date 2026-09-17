using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI.Views;

public partial class FindingsView : UserControl
{
    private readonly IServiceProvider _services;
    private List<FindingRow> _rows = new();

    public FindingsView(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => await LoadAsync();

    private async void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadAsync();
    }

    private void FindingsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DetailsButton.IsEnabled = FindingsGrid.SelectedItem is FindingRow;
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e) => OpenDetails();

    private void FindingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenDetails();

    private void OpenDetails()
    {
        if (FindingsGrid.SelectedItem is not FindingRow row) return;
        var win = new FindingDetailsWindow(row.Entity) { Owner = Window.GetWindow(this) };
        win.ShowDialog();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IFindingService>();

            Severity? severity = null;
            if (SeverityFilterCombo.SelectedItem is ComboBoxItem sevItem
                && sevItem.Tag is string sevTag
                && !string.IsNullOrEmpty(sevTag)
                && Enum.TryParse<Severity>(sevTag, out var sev))
            {
                severity = sev;
            }

            FindingStatus? status = null;
            if (StatusFilterCombo.SelectedItem is ComboBoxItem stItem
                && stItem.Tag is string stTag
                && !string.IsNullOrEmpty(stTag)
                && Enum.TryParse<FindingStatus>(stTag, out var st))
            {
                status = st;
            }

            var list = await service.SearchAsync(SearchBox.Text, severity, status, null);
            _rows = list.Select(FindingRow.From).ToList();
            FindingsGrid.ItemsSource = _rows;
            EmptyMessage.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = $"{_rows.Count} finding(s)";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load findings");
            StatusText.Text = "Failed to load findings.";
        }
    }

    private sealed class FindingRow
    {
        public Finding Entity { get; init; } = null!;
        public string Title => Entity.Title;
        public Severity Severity => Entity.Severity;
        public FindingStatus Status => Entity.Status;
        public string? AffectedPort => Entity.AffectedPort;
        public string? RuleId => Entity.RuleId;
        public DateTime CreatedAt => Entity.CreatedAt;
        public string HostDisplay { get; init; } = "—";

        public static FindingRow From(Finding f) => new()
        {
            Entity = f,
            HostDisplay = f.Host is null
                ? "—"
                : !string.IsNullOrWhiteSpace(f.Host.IpAddress)
                    ? f.Host.IpAddress
                    : f.Host.Hostname ?? f.Host.MacAddress ?? "—"
        };
    }
}
