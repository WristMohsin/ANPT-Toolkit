using System.Windows;
using System.Windows.Controls;
using ANPT.Application.Interfaces;
using ANPT.UI.Views;
using Serilog;

namespace ANPT.UI;

public partial class MainWindow : Window
{
    private readonly IApplicationInfo _appInfo;
    private readonly ICurrentUserService _currentUser;
    private Button? _activeNavButton;
    private bool _loggingOut;

    public MainWindow(IApplicationInfo appInfo, ICurrentUserService currentUser)
    {
        _appInfo = appInfo;
        _currentUser = currentUser;
        InitializeComponent();

        Title = $"{_appInfo.ShortName} — {_appInfo.ApplicationName}";
        VersionText.Text = $"v{_appInfo.Version}";

        if (_currentUser.IsAuthenticated && _currentUser.User is not null)
        {
            UserDisplayText.Text = _currentUser.DisplayName ?? _currentUser.Username ?? "User";
            RoleDisplayText.Text = (_currentUser.Role?.ToString() ?? "Viewer").ToUpperInvariant();
        }

        _activeNavButton = NavDashboard;
        ShowDashboard();
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Sign out of ANPT Toolkit?",
            "Logout",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _loggingOut = true;
        Log.Information("User {Username} logged out", _currentUser.Username);
        _currentUser.Clear();
        Close();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag)
            return;

        SetActiveNav(btn);

        switch (tag)
        {
            case "Dashboard":
                ShowDashboard();
                break;
            case "About":
                ContentArea.Content = new AboutView(_appInfo);
                break;
            case "Targets":
            case "Scans":
            case "Hosts":
            case "Services":
            case "Findings":
            case "Reports":
            case "Profiles":
            case "Logs":
            case "Settings":
                ContentArea.Content = CreatePlaceholder(tag);
                break;
            default:
                ContentArea.Content = CreatePlaceholder("Unknown");
                break;
        }
    }

    private void SetActiveNav(Button active)
    {
        if (_activeNavButton != null)
            _activeNavButton.Style = (Style)FindResource("NavButtonStyle");

        active.Style = (Style)FindResource("NavButtonActiveStyle");
        _activeNavButton = active;
    }

    private void ShowDashboard()
    {
        ContentArea.Content = new DashboardView();
    }

    private static UIElement CreatePlaceholder(string module)
    {
        var panel = new StackPanel { Margin = new Thickness(0) };
        panel.Children.Add(new TextBlock
        {
            Text = module,
            Style = (Style)Application.Current.FindResource("SectionHeaderStyle")
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"The {module} module will be implemented in a later phase.\n\nThis is the Phase 1–2 foundation shell.",
            Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("BrushTextSecondary"),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });
        return panel;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_loggingOut && _currentUser.IsAuthenticated)
        {
            Log.Information("Main window closed by user {Username}", _currentUser.Username);
            _currentUser.Clear();
        }
        base.OnClosed(e);
    }
}
