using System.Windows;
using System.Windows.Controls;
using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI;

public partial class ScanCreateWindow : Window
{
    private readonly IServiceProvider _services;
    private bool _isBusy;

    private sealed class ComboItem
    {
        public Guid Id { get; init; }
        public string Display { get; init; } = string.Empty;
        public Target? Target { get; init; }
        public ScanProfile? Profile { get; init; }
    }

    public ScanCreateWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        Loaded += async (_, _) => await LoadCombosAsync();
    }

    private async Task LoadCombosAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();

            var targets = await scanService.GetEligibleTargetsAsync();
            TargetCombo.ItemsSource = targets.Select(t => new ComboItem
            {
                Id = t.Id,
                Display = $"{t.Name} ({t.Address})",
                Target = t
            }).ToList();

            var profiles = await scanService.GetEnabledProfilesAsync();
            ProfileCombo.ItemsSource = profiles.Select(p => new ComboItem
            {
                Id = p.Id,
                Display = $"{p.Name} — {p.Description}",
                Profile = p
            }).ToList();

            if (TargetCombo.Items.Count == 0)
            {
                AuthHintText.Text = "No eligible targets. Create an Active target and confirm authorization first.";
                AuthHintText.Foreground = (System.Windows.Media.Brush)FindResource("BrushAccentWarning");
                CreateButton.IsEnabled = false;
            }
            else
            {
                TargetCombo.SelectedIndex = 0;
            }

            if (ProfileCombo.Items.Count > 0)
                ProfileCombo.SelectedIndex = 0;
            else
            {
                ShowError("No enabled scan profiles are available.");
                CreateButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load scan create combos");
            ShowError("Unable to load targets or profiles.");
            CreateButton.IsEnabled = false;
        }
    }

    private void TargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetCombo.SelectedItem is ComboItem item && item.Target is not null)
        {
            AuthHintText.Text = item.Target.AuthorizationConfirmed
                ? $"Authorization confirmed · {item.Target.Status}"
                : "Authorization not confirmed";
            AuthHintText.Foreground = (System.Windows.Media.Brush)FindResource(
                item.Target.AuthorizationConfirmed ? "BrushAccentPrimary" : "BrushAccentWarning");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        HideError();

        if (TargetCombo.SelectedItem is not ComboItem targetItem)
        {
            ShowError("Select a target.");
            return;
        }
        if (ProfileCombo.SelectedItem is not ComboItem profileItem)
        {
            ShowError("Select a scan profile.");
            return;
        }

        _isBusy = true;
        CreateButton.IsEnabled = false;
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IScanService>();
            var currentUser = _services.GetRequiredService<ICurrentUserService>();

            var result = await service.CreateAsync(new CreateScanRequest
            {
                TargetId = targetItem.Id,
                ScanProfileId = profileItem.Id,
                Name = NameBox.Text
            }, currentUser.Username);

            if (!result.Succeeded)
            {
                ShowError(result.ErrorMessage ?? "Unable to create scan.");
                return;
            }

            Log.Information("Scan created from UI: {Id}", result.Scan?.Id);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Create scan UI error");
            ShowError("Unable to create scan. Please try again.");
        }
        finally
        {
            _isBusy = false;
            CreateButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
    }
}
