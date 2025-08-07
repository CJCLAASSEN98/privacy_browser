using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EphemeralBrowser.Core.Services;
using EphemeralBrowser.UI.ViewModels;

namespace EphemeralBrowser.Tests.Integration;

public class BrowserIntegrationTests : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public BrowserIntegrationTests()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Core services
                services.AddSingleton<IUrlSanitizer, UrlSanitizer>();
                services.AddSingleton<IProfileManager, ProfileManager>();
                services.AddSingleton<IDownloadGate, DownloadGate>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<TabViewModel>();

                // Logging
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            })
            .Build();

        _services = _host.Services;
    }

    [Fact]
    public async Task DependencyInjection_ResolvesAllServices()
    {
        await _host.StartAsync();

        // Should be able to resolve all core services
        var urlSanitizer = _services.GetService<IUrlSanitizer>();
        var profileManager = _services.GetService<IProfileManager>();
        var downloadGate = _services.GetService<IDownloadGate>();

        urlSanitizer.Should().NotBeNull();
        profileManager.Should().NotBeNull();
        downloadGate.Should().NotBeNull();
    }

    [Fact]
    public async Task MainViewModel_Initialization_Succeeds()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        
        var act = async () => await mainViewModel.InitializeAsync();
        
        await act.Should().NotThrowAsync();
        
        mainViewModel.Tabs.Should().HaveCountGreaterThan(0, "Should create initial tab");
        mainViewModel.ActiveTab.Should().NotBeNull("Should have an active tab");
    }

    [Fact]
    public async Task MainViewModel_NewTab_CreatesEphemeralProfile()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync();
        
        var initialTabCount = mainViewModel.Tabs.Count;
        
        await mainViewModel.NewTabCommand.ExecuteAsync(null);
        
        mainViewModel.Tabs.Should().HaveCount(initialTabCount + 1);
        mainViewModel.ActiveTab.Should().NotBeNull();
        mainViewModel.ActiveTab!.ProfileId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UrlSanitization_Integration_WorksEndToEnd()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync();
        
        var testUrl = "https://example.com/page?utm_source=test&utm_medium=email&normal=param";
        
        // Navigate should sanitize the URL
        await mainViewModel.NavigateCommand.ExecuteAsync(testUrl);
        
        // Should show sanitization occurred
        mainViewModel.HasSanitizedUrl.Should().BeTrue();
        mainViewModel.StatusMessage.Should().Contain("tracking parameters");
        
        // The current URL should be sanitized
        mainViewModel.CurrentUrl.Should().Be("https://example.com/page?normal=param");
    }

    [Fact]
    public async Task ProfileManager_Integration_ManagesLifecycle()
    {
        await _host.StartAsync();

        var profileManager = _services.GetRequiredService<IProfileManager>();
        
        // Create profile
        var profileInfo = await profileManager.CreateEphemeralProfileAsync();
        profileInfo.Should().NotBeNull();
        profileInfo.IsActive.Should().BeTrue();
        
        // Get environment
        var environment = await profileManager.GetEnvironmentAsync(profileInfo.ProfileId);
        environment.Should().NotBeNull();
        
        // Verify it's in active profiles
        var activeProfiles = profileManager.GetActiveProfiles();
        activeProfiles.Should().Contain(p => p.ProfileId == profileInfo.ProfileId);
        
        // Dispose profile
        await profileManager.DisposeProfileAsync(profileInfo.ProfileId);
        
        // Should no longer be active
        var updatedActiveProfiles = profileManager.GetActiveProfiles();
        updatedActiveProfiles.Should().NotContain(p => p.ProfileId == profileInfo.ProfileId);
    }

    [Fact]
    public async Task MultipleTabsWithDifferentProfiles_WorkIndependently()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync();
        
        // Create additional tabs
        await mainViewModel.NewTabCommand.ExecuteAsync(null);
        await mainViewModel.NewTabCommand.ExecuteAsync(null);
        
        mainViewModel.Tabs.Should().HaveCount(3);
        
        // Each tab should have a unique profile
        var profileIds = mainViewModel.Tabs.Select(t => t.ProfileId).ToList();
        profileIds.Should().OnlyHaveUniqueItems();
        
        // All profiles should exist
        var profileManager = _services.GetRequiredService<IProfileManager>();
        var activeProfiles = profileManager.GetActiveProfiles();
        
        foreach (var profileId in profileIds)
        {
            activeProfiles.Should().Contain(p => p.ProfileId == profileId);
        }
    }

    [Fact]
    public async Task ApplicationCleanup_DisposesAllResources()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync();
        
        // Create multiple tabs
        await mainViewModel.NewTabCommand.ExecuteAsync(null);
        await mainViewModel.NewTabCommand.ExecuteAsync(null);
        
        var profileManager = _services.GetRequiredService<IProfileManager>();
        var initialActiveProfiles = profileManager.GetActiveProfiles();
        initialActiveProfiles.Should().HaveCountGreaterThan(0);
        
        // Cleanup should dispose all resources
        await mainViewModel.CleanupAsync();
        
        // All profiles should be disposed
        var finalActiveProfiles = profileManager.GetActiveProfiles();
        finalActiveProfiles.Should().BeEmpty("All profiles should be cleaned up");
    }

    [Fact]
    public async Task PerformanceRequirements_AreMet()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        
        // Cold start performance test
        var coldStartStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await mainViewModel.InitializeAsync();
        coldStartStopwatch.Stop();
        
        // Should meet cold start requirement (though we can't test full WebView2 initialization here)
        coldStartStopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Initialization should be reasonably fast");
        
        // URL sanitization performance test  
        var sanitizationStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await mainViewModel.NavigateCommand.ExecuteAsync("https://example.com/test?utm_source=test&utm_medium=email&utm_campaign=spring");
        sanitizationStopwatch.Stop();
        
        // Should meet sanitization overhead requirement from PRP (<150ms)
        sanitizationStopwatch.ElapsedMilliseconds.Should().BeLessThan(150, "URL sanitization should be fast");
    }

    [Fact]
    public async Task ErrorHandling_DoesNotCrashApplication()
    {
        await _host.StartAsync();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync();
        
        // Test navigation to invalid URL
        var act1 = async () => await mainViewModel.NavigateCommand.ExecuteAsync("invalid-url");
        await act1.Should().NotThrowAsync("Invalid URLs should be handled gracefully");
        
        // Test closing non-existent tab
        var act2 = async () => await mainViewModel.CloseTabCommand.ExecuteAsync(null);
        await act2.Should().NotThrowAsync("Null tab close should be handled gracefully");
        
        // Application should still be functional
        mainViewModel.Tabs.Should().HaveCountGreaterThan(0);
        mainViewModel.ActiveTab.Should().NotBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}