using System.Windows;
using ANPT.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI;

public partial class ScanDetailsWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly Guid _scanId;

    public ScanDetailsWindow(IServiceProvider services, Guid scanId)
    {
        _services = services;
        _scanId = scanId;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScanService>();
            var scan = await service.GetByIdAsync(_scanId);
            if (scan is null)
            {
                MessageBox.Show("Scan not found.", "Scan Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            NameText.Text = scan.Name;
            IdText.Text = scan.Id.ToString();
            StatusText.Text = scan.Status.ToString();
            TargetText.Text = scan.Target?.Name ?? scan.TargetId.ToString();
            AddressText.Text = scan.Target?.Address ?? "—";
            AuthText.Text = scan.Target is null
                ? "—"
                : (scan.Target.AuthorizationConfirmed ? "Confirmed" : "Not confirmed");
            ProfileText.Text = scan.ScanProfile?.Name ?? scan.ScanProfileId.ToString();
            CreatedText.Text = scan.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            StartedText.Text = scan.StartedAt.HasValue ? scan.StartedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "—";
            CompletedText.Text = scan.CompletedAt.HasValue ? scan.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "—";

            if (scan.StartedAt.HasValue && scan.CompletedAt.HasValue)
            {
                var duration = scan.CompletedAt.Value - scan.StartedAt.Value;
                DurationText.Text = duration.TotalSeconds < 60
                    ? $"{duration.TotalSeconds:0}s"
                    : $"{duration.TotalMinutes:0.1} min";
            }
            else
            {
                DurationText.Text = "—";
            }

            MessageText.Text = scan.StatusMessage ?? scan.ErrorMessage ?? "—";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load scan details {Id}", _scanId);
            MessageBox.Show("Unable to load scan details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
