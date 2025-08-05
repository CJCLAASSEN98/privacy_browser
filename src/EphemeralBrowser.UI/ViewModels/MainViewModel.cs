using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using EphemeralBrowser.Core.Services;

namespace EphemeralBrowser.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProfileManager _profileManager;
    private readonly IUrlSanitizer _urlSanitizer;
    private readonly IDownloadGate _downloadGate;
    private readonly ILogger<MainViewModel> _logger;
    
    [ObservableProperty]
    private ObservableCollection<TabViewModel> tabs = new();
    
    [ObservableProperty]
    private TabViewModel? activeTab;
    
    [ObservableProperty]
    private string currentUrl = "https://";
    
    [ObservableProperty]
    private string statusMessage = "Ready";
    
    [ObservableProperty]
    private string performanceInfo = "";
    
    [ObservableProperty]
    private bool hasSanitizedUrl = false;

    public MainViewModel(
        IProfileManager profileManager,
        IUrlSanitizer urlSanitizer,
        IDownloadGate downloadGate,
        ILogger<MainViewModel> logger)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _urlSanitizer = urlSanitizer ?? throw new ArgumentNullException(nameof(urlSanitizer));
        _downloadGate = downloadGate ?? throw new ArgumentNullException(nameof(downloadGate));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize commands
        NavigateCommand = new AsyncRelayCommand<string>(NavigateAsync);
        NewTabCommand = new AsyncRelayCommand(CreateNewTabAsync);
        CloseTabCommand = new AsyncRelayCommand<TabViewModel>(CloseTabAsync);

        // Start performance monitoring
        StartPerformanceMonitoring();
    }

    public IAsyncRelayCommand<string> NavigateCommand { get; }
    public IAsyncRelayCommand NewTabCommand { get; }
    public IAsyncRelayCommand<TabViewModel> CloseTabCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing MainViewModel...");
            
            // Create first tab
            await CreateNewTabAsync();
            
            StatusMessage = "EphemeralBrowser ready - Privacy first browsing";
            
            _logger.LogInformation("MainViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MainViewModel");
            StatusMessage = "Failed to initialize browser";
            throw;
        }
    }

    public async Task CleanupAsync()
    {
        try
        {
            _logger.LogInformation("Cleaning up MainViewModel...");
            
            var cleanupTasks = Tabs.Select(tab => tab.DisposeAsync().AsTask()).ToArray();
            await Task.WhenAll(cleanupTasks);
            
            Tabs.Clear();
            
            _logger.LogInformation("MainViewModel cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MainViewModel cleanup");
        }
    }

    private async Task NavigateAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || ActiveTab == null)
            return;

        try
        {
            // Ensure URL has protocol
            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("about:"))
            {
                url = "https://" + url;
            }

            // Apply URL sanitization
            var sanitizationResult = _urlSanitizer.Sanitize(url);
            
            if (sanitizationResult.RemovedParams.Length > 0)
            {
                HasSanitizedUrl = true;
                StatusMessage = $"Removed {sanitizationResult.RemovedParams.Length} tracking parameters";
                _logger.LogInformation("URL sanitized: removed {Count} parameters", sanitizationResult.RemovedParams.Length);
            }
            else
            {
                HasSanitizedUrl = false;
            }

            // Navigate to sanitized URL
            await ActiveTab.NavigateAsync(sanitizationResult.SanitizedUrl);
            CurrentUrl = sanitizationResult.SanitizedUrl;
            
            _logger.LogDebug("Navigation initiated to: {Url}", sanitizationResult.SanitizedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for URL: {Url}", url);
            StatusMessage = $"Navigation failed: {ex.Message}";
        }
    }

    private async Task CreateNewTabAsync()
    {
        try
        {
            _logger.LogInformation("Creating new tab...");
            
            // Create ephemeral profile
            var profileInfo = await _profileManager.CreateEphemeralProfileAsync();
            
            // Create tab view model
            var tabViewModel = new TabViewModel(
                profileInfo,
                _profileManager,
                _urlSanitizer,
                _downloadGate,
                _logger);

            // Initialize the tab
            await tabViewModel.InitializeAsync();
            
            // Add to collection and set as active
            Tabs.Add(tabViewModel);
            ActiveTab = tabViewModel;
            
            // Subscribe to tab events
            tabViewModel.PropertyChanged += OnTabPropertyChanged;
            
            StatusMessage = $"New container created - {profileInfo.ProfileId[..8]}...";
            _logger.LogInformation("New tab created with profile: {ProfileId}", profileInfo.ProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new tab");
            StatusMessage = $"Failed to create container: {ex.Message}";
        }
    }

    private async Task CloseTabAsync(TabViewModel? tab)
    {
        if (tab == null)
            return;

        try
        {
            _logger.LogInformation("Closing tab: {Title}", tab.Title);
            
            // Unsubscribe from events
            tab.PropertyChanged -= OnTabPropertyChanged;
            
            // Remove from collection
            Tabs.Remove(tab);
            
            // Dispose tab resources
            await tab.DisposeAsync();
            
            // Select another tab or create new one
            if (Tabs.Count == 0)
            {
                await CreateNewTabAsync();
            }
            else if (ActiveTab == tab)
            {
                ActiveTab = Tabs.Last();
            }
            
            StatusMessage = "Container closed and wiped";
            _logger.LogInformation("Tab closed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close tab");
            StatusMessage = $"Failed to close container: {ex.Message}";
        }
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is TabViewModel tab && tab == ActiveTab)
        {
            switch (e.PropertyName)
            {
                case nameof(TabViewModel.Url):
                    CurrentUrl = tab.Url;
                    break;
                case nameof(TabViewModel.Title):
                    OnPropertyChanged(nameof(ActiveTab));
                    break;
                case nameof(TabViewModel.IsLoading):
                    if (tab.IsLoading)
                        StatusMessage = $"Loading {tab.Url}...";
                    else
                        StatusMessage = "Ready";
                    break;
            }
        }
    }

    private void StartPerformanceMonitoring()
    {
        var timer = new System.Timers.Timer(1000); // Update every second
        timer.Elapsed += (_, _) =>
        {
            try
            {
                var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                var tabCount = Tabs.Count;
                var activeProfiles = _profileManager.GetActiveProfiles().Length;
                
                PerformanceInfo = $"Memory: {memoryMB:F1} MB | Containers: {tabCount} | Profiles: {activeProfiles}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Performance monitoring error");
            }
        };
        timer.Start();
    }
}