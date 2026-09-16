using System.Windows;
using System.Windows.Controls;
using ANPT.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ANPT.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly IServiceProvider _services;

    public DashboardView(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        Loaded += async (_, _) => await LoadMetricsAsync();
    }

    private async Task LoadMetricsAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var targetService = scope.ServiceProvider.GetRequiredService<ITargetService>();
            var count = await targetService.GetTotalCountAsync();
            TotalTargetsValue.Text = count.ToString();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load dashboard target count");
            TotalTargetsValue.Text = "—";
        }
    }
}
