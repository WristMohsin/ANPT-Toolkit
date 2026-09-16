using System.Windows;
using System.Windows.Controls;
using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI;

public partial class TargetEditWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly Target? _existing;
    private readonly bool _isEdit;
    private bool _isBusy;

    public TargetEditWindow(IServiceProvider services, Target? existing)
    {
        _services = services;
        _existing = existing;
        _isEdit = existing is not null;
        InitializeComponent();

        if (_isEdit && existing is not null)
        {
            HeaderText.Text = "Edit Target";
            Title = "Edit Target";
            NameBox.Text = existing.Name;
            AddressBox.Text = existing.Address;
            DescriptionBox.Text = existing.Description ?? string.Empty;
            AuthCheckBox.IsChecked = existing.AuthorizationConfirmed;

            SelectComboItem(TypeCombo, existing.TargetType);
            StatusLabel.Visibility = Visibility.Visible;
            StatusCombo.Visibility = Visibility.Visible;
            SelectStatusCombo(existing.Status);
        }
        else
        {
            HeaderText.Text = "New Target";
            Title = "New Target";
        }

        Loaded += (_, _) => NameBox.Focus();
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void SelectStatusCombo(TargetStatus status)
    {
        foreach (ComboBoxItem item in StatusCombo.Items)
        {
            if (item.Tag is string tag && string.Equals(tag, status.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                StatusCombo.SelectedItem = item;
                return;
            }
        }
        StatusCombo.SelectedIndex = 0;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        HideError();
        _isBusy = true;
        SaveButton.IsEnabled = false;

        try
        {
            var name = NameBox.Text?.Trim() ?? string.Empty;
            var address = AddressBox.Text?.Trim() ?? string.Empty;
            var type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Host";
            var description = DescriptionBox.Text;
            var auth = AuthCheckBox.IsChecked == true;

            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITargetService>();
            var currentUser = _services.GetRequiredService<ICurrentUserService>();

            TargetServiceResult result;

            if (_isEdit && _existing is not null)
            {
                var status = TargetStatus.Active;
                if (StatusCombo.SelectedItem is ComboBoxItem statusItem &&
                    statusItem.Tag is string statusTag &&
                    Enum.TryParse<TargetStatus>(statusTag, out var parsed))
                {
                    status = parsed;
                }

                result = await service.UpdateAsync(new UpdateTargetRequest
                {
                    Id = _existing.Id,
                    Name = name,
                    Address = address,
                    TargetType = type,
                    Description = description,
                    AuthorizationConfirmed = auth,
                    Status = status
                });
            }
            else
            {
                result = await service.CreateAsync(new CreateTargetRequest
                {
                    Name = name,
                    Address = address,
                    TargetType = type,
                    Description = description,
                    AuthorizationConfirmed = auth
                }, currentUser.Username);
            }

            if (!result.Succeeded)
            {
                ShowError(result.ErrorMessage ?? "Unable to save target.");
                return;
            }

            Log.Information("Target saved from UI: {Name}, Auth={Auth}", name, auth);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Save target UI error");
            ShowError("Unable to save target. Please try again.");
        }
        finally
        {
            _isBusy = false;
            SaveButton.IsEnabled = true;
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
