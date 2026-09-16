using System.Windows;
using ANPT.Application;
using ANPT.Application.Interfaces;
using ANPT.Infrastructure;
using ANPT.Infrastructure.Data;
using ANPT.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ANPT.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);

        LoggingSetup.Configure();
        Log.Information("ANPT Toolkit starting...");

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddApplication();
                    services.AddInfrastructure();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            using (var scope = _host.Services.CreateScope())
            {
                var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                await initializer.InitializeAsync();
            }

            await RunAuthenticationLoopAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start.");
            MessageBox.Show(
                $"Unable to start ANPT Toolkit.\n\nReason:\n{ex.Message}\n\nSee logs for details.",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task RunAuthenticationLoopAsync()
    {
        while (true)
        {
            var loginWindow = _host!.Services.GetRequiredService<LoginWindow>();
            var loginResult = loginWindow.ShowDialog();

            if (loginResult != true)
            {
                Log.Information("Login cancelled or closed. Shutting down.");
                Shutdown(0);
                return;
            }

            var currentUser = _host.Services.GetRequiredService<ICurrentUserService>();
            if (!currentUser.IsAuthenticated)
            {
                Log.Warning("Login dialog returned success but session is empty.");
                continue;
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            var tcs = new TaskCompletionSource<bool>();
            mainWindow.Closed += (_, _) => tcs.TrySetResult(true);
            await tcs.Task;

            currentUser.Clear();
            Log.Information("Main window closed; session cleared.");

            if (ShutdownMode == ShutdownMode.OnExplicitShutdown)
            {
                if (!Current.Windows.OfType<Window>().Any())
                {
                    continue;
                }
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("ANPT Toolkit shutting down...");
        try
        {
            var currentUser = _host?.Services.GetService<ICurrentUserService>();
            currentUser?.Clear();
        }
        catch
        {
        }

        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
