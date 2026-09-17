using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ANPT.Application.Interfaces;
using ANPT.Application.Models.Findings;
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
        FindingsGrid.SelectionChanged += (_, _) =>
            DetailsButton.IsEnabled = FindingsGrid.SelectedItem is FindingRow;
    }

    private async void Filter_Changed(object sender, EventArgs e)
    {
        if (!IsLoaded) return;
        await LoadAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SeverityFilterCombo.SelectedIndex = 0;
        StatusFilterCombo.SelectedIndex = 0;
        RuleFilterCombo.SelectedIndex = 0;
        await LoadAsync();
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e) => OpenDetails();
    private void FindingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenDetails();

    private async void OpenDetails()
    {
        if (FindingsGrid.SelectedItem is not FindingRow row) return;
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IFindingService>();
            var detail = await service.GetDetailAsync(row.Entity.Id);
            if (detail is null)
            {
                MessageBox.Show("Finding not found.", "Finding Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var win = new FindingDetailsWindow(detail) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open finding details");
            MessageBox.Show("Unable to open finding details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

            string? ruleId = null;
            if (RuleFilterCombo.SelectedItem is ComboBoxItem ruleItem
                && ruleItem.Tag is string ruleTag
                && !string.IsNullOrEmpty(ruleTag))
            {
                ruleId = ruleTag;
            }

            var list = await service.SearchAsync(SearchBox.Text, severity, status, null, ruleId);
            var summary = await service.GetSummaryAsync();
            _rows = list.Select(FindingRow.From).ToList();
            FindingsGrid.ItemsSource = _rows;
            EmptyMessage.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text =
                $"{_rows.Count} shown · Total {summary.Total} · Open {summary.Open} · High/Critical {summary.High + summary.Critical}";
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
        public FindingPriority Priority { get; init; }
        public string? AffectedPort => Entity.AffectedPort;
        public string? RuleId => Entity.RuleId;
        public DateTime CreatedAt => Entity.CreatedAt;
        public string HostDisplay { get; init; } = "-";

        public static FindingRow From(Finding f)
        {
            var risk = FindingRiskContext.FromSeverity(f.Severity, f.RuleId);
            return new FindingRow
            {
                Entity = f,
                Priority = risk.Priority,
                HostDisplay = f.Host is null
                    ? "-"
                    : !string.IsNullOrWhiteSpace(f.Host.IpAddress)
                        ? f.Host.IpAddress
                        : f.Host.Hostname ?? f.Host.MacAddress ?? "-"
            };
        }
    }
}
