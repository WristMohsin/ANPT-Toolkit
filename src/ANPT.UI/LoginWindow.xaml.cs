using System.Windows;
using System.Windows.Input;
using ANPT.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI;

public partial class LoginWindow : Window
{
    private readonly IServiceProvider _services;
    private bool _isBusy;

    public LoginWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await AttemptLoginAsync();
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await AttemptLoginAsync();
        }
    }

    private async Task AttemptLoginAsync()
    {
        if (_isBusy)
            return;

        var username = UsernameBox.Text?.Trim() ?? string.Empty;
        var password = PasswordBox.Password ?? string.Empty;

        HideError();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Enter username and password.");
            return;
        }

        _isBusy = true;
        LoginButton.IsEnabled = false;
        LoginButton.Content = "Signing in…";

        try
        {
            using var scope = _services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var currentUser = _services.GetRequiredService<ICurrentUserService>();

            var result = await auth.LoginAsync(username, password);

            PasswordBox.Password = string.Empty;

            if (!result.Succeeded || result.User is null)
            {
                ShowError(result.ErrorMessage ?? "Invalid username or password.");
                PasswordBox.Focus();
                return;
            }

            currentUser.SetUser(result.User);
            Log.Information("User session established for {Username}", result.User.Username);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Login UI error");
            ShowError("Unable to sign in. Please try again.");
            PasswordBox.Password = string.Empty;
        }
        finally
        {
            _isBusy = false;
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Sign In";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Text = string.Empty;
        ErrorText.Visibility = Visibility.Collapsed;
    }
}
