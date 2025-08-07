using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EphemeralBrowser.Core.Services;
using EphemeralBrowser.UI.Models;

namespace EphemeralBrowser.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProfileManager _profileManager;
    private readonly IUrlSanitizer _urlSanitizer;
    private readonly IDownloadGate _downloadGate;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainViewModel> _logger;
    
    [ObservableProperty]
    private ObservableCollection<TabViewModel> _tabs = new();
    
    [ObservableProperty]
    private TabViewModel? _activeTab;
    
    [ObservableProperty]
    private string _currentUrl = "about:blank";
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private string _performanceInfo = "";
    
    [ObservableProperty]
    private bool _hasSanitizedUrl;

    [ObservableProperty]
    private bool _isPanelExpanded;
    
    [ObservableProperty]
    private ObservableCollection<DownloadItem> _quarantinedDownloads = new();
    
    [ObservableProperty]
    private ObservableCollection<SanitizationRule> _sanitizationRules = new();
    
    [ObservableProperty]
    private ObservableCollection<NavigationEvent> _navigationEvents = new();
    
    [ObservableProperty]
    private string _sanitizationStats = "0 rules active";
    
    [ObservableProperty]
    private string _motwStatus = "Active";
    
    [ObservableProperty]
    private string _quarantineDirectory = "";

    // Download metrics properties
    public int TotalDownloads => QuarantinedDownloads.Count;
    public int QuarantinedDownloadsCount => QuarantinedDownloads.Count(d => d.Status == "Quarantined");
    public int PromotedDownloads => QuarantinedDownloads.Count(d => d.Status == "Promoted");
    public long TotalBytesDownloaded => QuarantinedDownloads.Sum(d => d.Size);
    
    // Computed properties for status
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasActiveDownloads => QuarantinedDownloads.Any(d => d.Status == "Downloading");
    public string ActiveDownloadsText => $"{QuarantinedDownloads.Count(d => d.Status == "Downloading")} downloading";

    public bool CanGoBack => ActiveTab?.CanGoBack() == true;
    public bool CanGoForward => ActiveTab?.CanGoForward() == true;

    public MainViewModel(
        IProfileManager profileManager,
        IUrlSanitizer urlSanitizer,
        IDownloadGate downloadGate,
        IServiceProvider serviceProvider,
        ILogger<MainViewModel> logger)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _urlSanitizer = urlSanitizer ?? throw new ArgumentNullException(nameof(urlSanitizer));
        _downloadGate = downloadGate ?? throw new ArgumentNullException(nameof(downloadGate));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize commands
        NavigateCommand = new AsyncRelayCommand<string>(NavigateAsync);
        NewTabCommand = new AsyncRelayCommand(CreateNewTabAsync);
        CloseTabCommand = new AsyncRelayCommand<TabViewModel>(CloseTabAsync);
        SelectTabCommand = new RelayCommand<TabViewModel>(SelectTab);
        GoBackCommand = new RelayCommand(() => {
            _logger.LogInformation("GoBackCommand executed - ActiveTab: {HasActiveTab}", ActiveTab != null);
            if (ActiveTab != null)
            {
                _logger.LogInformation("ActiveTab details - ProfileId: {ProfileId}, CanGoBack: {CanGoBack}", 
                    ActiveTab.ProfileId, ActiveTab.CanGoBack());
                ActiveTab.GoBack();
            }
            else
            {
                _logger.LogWarning("GoBackCommand executed but ActiveTab is null");
            }
        }, () => {
            var canGoBack = CanGoBack;
            _logger.LogDebug("GoBackCommand CanExecute: {CanGoBack} (ActiveTab: {HasActiveTab})", 
                canGoBack, ActiveTab != null);
            return canGoBack;
        });
        GoForwardCommand = new RelayCommand(() => {
            _logger.LogInformation("GoForwardCommand executed - ActiveTab: {HasActiveTab}, CanGoForward: {CanGoForward}", 
                ActiveTab != null, ActiveTab?.CanGoForward());
            ActiveTab?.GoForward();
        }, () => {
            var canGoForward = CanGoForward;
            _logger.LogDebug("GoForwardCommand CanExecute: {CanGoForward}", canGoForward);
            return canGoForward;
        });
        RefreshCommand = new AsyncRelayCommand(() => ActiveTab?.RefreshCommand.ExecuteAsync(null) ?? Task.CompletedTask);
        
        // Download commands
        PromoteDownloadCommand = new RelayCommand<DownloadItem>(PromoteDownload);
        DeleteDownloadCommand = new RelayCommand<DownloadItem>(DeleteDownload);
        ClearQuarantineCommand = new AsyncRelayCommand(ClearQuarantineAsync);
        
        // Rules commands
        LoadRulesCommand = new AsyncRelayCommand(LoadRulesAsync);
        ExportRulesCommand = new AsyncRelayCommand(ExportRulesAsync);
        ResetRulesCommand = new AsyncRelayCommand(ResetRulesAsync);
        TestUrlCommand = new AsyncRelayCommand(TestUrlAsync);
        
        // Diagnostics commands
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync);
        ForceGcCommand = new RelayCommand(ForceGc);

        // Start performance monitoring
        StartPerformanceMonitoring();
    }

    public IAsyncRelayCommand<string> NavigateCommand { get; }
    public IAsyncRelayCommand NewTabCommand { get; }
    public IAsyncRelayCommand<TabViewModel> CloseTabCommand { get; }
    public IRelayCommand<TabViewModel> SelectTabCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand GoForwardCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    
    // Download commands
    public IRelayCommand<DownloadItem> PromoteDownloadCommand { get; }
    public IRelayCommand<DownloadItem> DeleteDownloadCommand { get; }
    public IAsyncRelayCommand ClearQuarantineCommand { get; }
    
    // Rules commands  
    public IAsyncRelayCommand LoadRulesCommand { get; }
    public IAsyncRelayCommand ExportRulesCommand { get; }
    public IAsyncRelayCommand ResetRulesCommand { get; }
    public IAsyncRelayCommand TestUrlCommand { get; }
    
    // Diagnostics commands
    public IAsyncRelayCommand ExportDiagnosticsCommand { get; }
    public IRelayCommand ForceGcCommand { get; }

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
            
            // Create tab view model directly (temporary approach)
            var logger = _serviceProvider.GetRequiredService<ILogger<TabViewModel>>();
            var tabViewModel = new TabViewModel(
                profileInfo,
                _profileManager,
                _urlSanitizer,
                _downloadGate,
                logger);

            // Initialize the tab
            await tabViewModel.InitializeAsync();
            
            // Add to collection and set as active
            Tabs.Add(tabViewModel);
            
            // Deactivate all other tabs and activate this one
            foreach (var existingTab in Tabs.Where(t => t != tabViewModel))
            {
                existingTab.IsActive = false;
            }
            
            tabViewModel.IsActive = true;
            ActiveTab = tabViewModel;
            
            // Subscribe to tab events
            tabViewModel.PropertyChanged += OnTabPropertyChanged;
            
            // Update command states for the new active tab
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
            
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
                var newActiveTab = Tabs.Last();
                newActiveTab.IsActive = true;
                ActiveTab = newActiveTab;
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

    private void SelectTab(TabViewModel? tab)
    {
        if (tab == null || tab == ActiveTab)
        {
            _logger.LogDebug("SelectTab called but tab is null or already active");
            return;
        }

        try
        {
            _logger.LogInformation("Selecting tab: {Title} (Profile: {ProfileId})", tab.Title, tab.ProfileId);
            
            // Deactivate all tabs
            foreach (var existingTab in Tabs)
            {
                existingTab.IsActive = false;
            }
            
            // Activate the selected tab
            tab.IsActive = true;
            ActiveTab = tab;
            CurrentUrl = tab.Url;
            StatusMessage = $"Switched to container {tab.ProfileId[..8]}...";
            
            // Force property change notifications
            OnPropertyChanged(nameof(ActiveTab));
            OnPropertyChanged(nameof(CurrentUrl));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            
            // Update command states for the new active tab
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
            
            _logger.LogInformation("Tab selection completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select tab");
            StatusMessage = $"Failed to switch container: {ex.Message}";
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
                    // Navigation state might have changed
                    GoBackCommand.NotifyCanExecuteChanged();
                    GoForwardCommand.NotifyCanExecuteChanged();
                    break;
                case nameof(TabViewModel.Title):
                    OnPropertyChanged(nameof(ActiveTab));
                    break;
                case nameof(TabViewModel.IsLoading):
                    if (tab.IsLoading)
                        StatusMessage = $"Loading {tab.Url}...";
                    else
                    {
                        StatusMessage = "Ready";
                        // Navigation completed, update command states
                        GoBackCommand.NotifyCanExecuteChanged();
                        GoForwardCommand.NotifyCanExecuteChanged();
                    }
                    break;
                case "NavigationStateChanged":
                    // CRITICAL: Update MainViewModel commands when tab navigation state changes
                    _logger.LogInformation("Navigation state changed in tab - updating MainViewModel commands");
                    GoBackCommand.NotifyCanExecuteChanged();
                    GoForwardCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
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
                var memoryMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                var tabCount = Tabs.Count;
                var activeProfiles = _profileManager.GetActiveProfiles().Length;
                
                PerformanceInfo = $"Memory: {memoryMb:F1} MB | Containers: {tabCount} | Profiles: {activeProfiles}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Performance monitoring error");
            }
        };
        timer.Start();
    }

    // Command implementations
    private void PromoteDownload(DownloadItem? download)
    {
        if (download == null) return;
        
        // TODO: Implement download promotion
        _logger.LogInformation("Promote download requested: {FileName}", download.FileName);
    }

    private void DeleteDownload(DownloadItem? download)
    {
        if (download == null) return;
        
        // TODO: Implement download deletion  
        QuarantinedDownloads.Remove(download);
        _logger.LogInformation("Delete download requested: {FileName}", download.FileName);
    }

    private async Task ClearQuarantineAsync()
    {
        // TODO: Implement quarantine clearing
        _logger.LogInformation("Clear quarantine requested");
        await Task.CompletedTask;
    }

    private async Task LoadRulesAsync()
    {
        // TODO: Implement rules loading
        _logger.LogInformation("Load rules requested");
        await Task.CompletedTask;
    }

    private async Task ExportRulesAsync()
    {
        // TODO: Implement rules export
        _logger.LogInformation("Export rules requested");
        await Task.CompletedTask;
    }

    private async Task ResetRulesAsync()
    {
        // TODO: Implement rules reset
        _logger.LogInformation("Reset rules requested");
        await Task.CompletedTask;
    }

    private async Task TestUrlAsync()
    {
        // TODO: Implement URL testing
        _logger.LogInformation("Test URL requested");
        await Task.CompletedTask;
    }

    private async Task ExportDiagnosticsAsync()
    {
        // TODO: Implement diagnostics export
        _logger.LogInformation("Export diagnostics requested");
        await Task.CompletedTask;
    }

    private void ForceGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _logger.LogInformation("Forced garbage collection");
    }
}