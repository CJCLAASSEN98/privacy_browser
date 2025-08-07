using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EphemeralBrowser.Core.Services;
using EphemeralBrowser.UI.ViewModels;

namespace EphemeralBrowser.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Configure services and dependency injection
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            await _host.StartAsync();

            // Create and show main window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application startup failed: {ex.Message}", "Startup Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            // Log shutdown error but don't prevent exit
            System.Diagnostics.Debug.WriteLine($"Shutdown error: {ex.Message}");
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IUrlSanitizer, UrlSanitizer>();
        services.AddSingleton<IProfileManager, ProfileManager>();
        services.AddSingleton<IDownloadGate, DownloadGate>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<TabViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        // Configuration
        services.AddOptions();

        // Logging
        services.AddLogging();
    }
}