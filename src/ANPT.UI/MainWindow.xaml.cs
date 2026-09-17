using System.Windows;
using System.Windows.Controls;
using ANPT.Application.Interfaces;
using ANPT.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ANPT.UI;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IApplicationInfo _appInfo;
    private readonly ICurrentUserService _currentUser;
    private Button? _activeNavButton;

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        _appInfo = services.GetRequiredService<IApplicationInfo>();
        _currentUser = services.GetRequiredService<ICurrentUserService>();
        InitializeComponent();
        Title = _appInfo.ProductName;
        Loaded += (_, _) =>
        {
            ShowDashboard();
            SetActiveNav(NavDashboard);
        };
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
                ContentArea.Content = new TargetsView(_services);
                break;
            case "Scans":
                ContentArea.Content = new ScansView(_services);
                break;
            case "Findings":
                ContentArea.Content = new FindingsView(_services);
                break;
            case "Hosts":
            case "Services":
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
        ContentArea.Content = new DashboardView(_services);
    }

    private static UIElement CreatePlaceholder(string name)
    {
        return new TextBlock
        {
            Text = $"{name} — coming in a later phase",
            Margin = new Thickness(24),
            FontSize = 16,
            Opacity = 0.7
        };
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _currentUser.Clear();
        var login = new LoginWindow(_services);
        login.Show();
        Close();
    }
}
