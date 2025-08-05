using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using EphemeralBrowser.Core.Services;
using System.IO;

namespace EphemeralBrowser.UI.ViewModels;

public partial class TabViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ProfileInfo _profileInfo;
    private readonly IProfileManager _profileManager;
    private readonly IUrlSanitizer _urlSanitizer;
    private readonly IDownloadGate _downloadGate;
    private readonly ILogger _logger;
    
    private CoreWebView2Environment? _environment;
    private CoreWebView2? _coreWebView2;
    private bool _disposed;
    
    [ObservableProperty]
    private string title = "New Container";
    
    [ObservableProperty]
    private string url = "about:blank";
    
    [ObservableProperty]
    private string loadingUrl = "";
    
    [ObservableProperty]
    private bool isLoading = true;
    
    [ObservableProperty]
    private bool isLoaded = false;
    
    [ObservableProperty]
    private bool hasSecurityWarning = false;
    
    [ObservableProperty]
    private string securityStatus = "Secure";

    public TabViewModel(
        ProfileInfo profileInfo,
        IProfileManager profileManager,
        IUrlSanitizer urlSanitizer,
        IDownloadGate downloadGate,
        ILogger logger)
    {
        _profileInfo = profileInfo;
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _urlSanitizer = urlSanitizer ?? throw new ArgumentNullException(nameof(urlSanitizer));
        _downloadGate = downloadGate ?? throw new ArgumentNullException(nameof(downloadGate));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize commands
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        GoBackCommand = new RelayCommand(GoBack, CanGoBack);
        GoForwardCommand = new RelayCommand(GoForward, CanGoForward);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand GoForwardCommand { get; }

    public string ProfileId => _profileInfo.ProfileId;

    public async Task<CoreWebView2Environment?> GetEnvironmentAsync()
    {
        return await _profileManager.GetEnvironmentAsync(_profileInfo.ProfileId);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing TabViewModel for profile: {ProfileId}", _profileInfo.ProfileId);
            
            // Get the WebView2 environment for this profile
            _environment = await _profileManager.GetEnvironmentAsync(_profileInfo.ProfileId);
            
            if (_environment == null)
            {
                throw new InvalidOperationException($"Failed to get environment for profile {_profileInfo.ProfileId}");
            }

            LoadingUrl = "Initializing secure container...";
            
            // Note: CoreWebView2 will be created when WebView2 control is initialized
            // For now, just mark as ready for WebView2 control creation
            
            IsLoading = false;
            IsLoaded = true;
            
            Title = "Secure Container";
            
            _logger.LogInformation("TabViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TabViewModel");
            throw;
        }
    }

    public async Task NavigateAsync(string url)
    {
        try
        {
            if (_coreWebView2 == null)
            {
                _logger.LogWarning("Attempted navigation before WebView2 is ready");
                return;
            }

            IsLoading = true;
            LoadingUrl = url;
            
            // Enforce HTTPS-only navigation (with exceptions)
            if (!url.StartsWith("https://") && !url.StartsWith("about:") && !url.StartsWith("data:"))
            {
                if (url.StartsWith("http://"))
                {
                    url = url.Replace("http://", "https://");
                    HasSecurityWarning = true;
                    SecurityStatus = "Upgraded to HTTPS";
                }
                else
                {
                    HasSecurityWarning = true;
                    SecurityStatus = "Insecure Protocol";
                }
            }
            else
            {
                HasSecurityWarning = false;
                SecurityStatus = "Secure";
            }

            await _coreWebView2.NavigateAsync(url);
            
            _logger.LogDebug("Navigation initiated to: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed");
            IsLoading = false;
            SecurityStatus = "Navigation Failed";
            HasSecurityWarning = true;
        }
    }

    public async Task SetupWebView2Async(CoreWebView2 coreWebView2)
    {
        try
        {
            _coreWebView2 = coreWebView2;
            
            // Configure privacy settings
            await ConfigurePrivacySettingsAsync();
            
            // Setup event handlers
            SetupEventHandlers();
            
            // Initialize download gate
            var quarantineDir = Path.Combine(_profileInfo.UserDataFolder, "Quarantine");
            await _downloadGate.InitializeAsync(_coreWebView2, quarantineDir);
            
            // Inject anti-fingerprinting shims
            await InjectPrivacyShimsAsync();
            
            _logger.LogInformation("WebView2 setup completed for profile: {ProfileId}", _profileInfo.ProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup WebView2");
            throw;
        }
    }

    private async Task ConfigurePrivacySettingsAsync()
    {
        if (_coreWebView2 == null) return;

        try
        {
            var settings = _coreWebView2.Settings;
            
            // Disable data persistence features
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = true; // Allow for user experience
            settings.AreDevToolsEnabled = false; // Disable for security
            settings.IsWebMessageEnabled = true; // Enable for privacy controls
            
            // Configure profile for strict privacy
            var profile = _coreWebView2.Profile;
            profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Strict;
            
            // Clear any existing data
            await profile.ClearBrowsingDataAsync();
            
            _logger.LogDebug("Privacy settings configured");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure privacy settings");
        }
    }

    private void SetupEventHandlers()
    {
        if (_coreWebView2 == null) return;

        try
        {
            _coreWebView2.NavigationStarting += OnNavigationStarting;
            _coreWebView2.NavigationCompleted += OnNavigationCompleted;
            _coreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
            _coreWebView2.SourceChanged += OnSourceChanged;
            
            _logger.LogDebug("Event handlers configured");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup event handlers");
        }
    }

    private async Task InjectPrivacyShimsAsync()
    {
        if (_coreWebView2 == null) return;

        try
        {
            // Load anti-fingerprinting shims
            var shimsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AntiFpShims.bundle.js");
            
            if (File.Exists(shimsPath))
            {
                var shimsScript = await File.ReadAllTextAsync(shimsPath);
                await _coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(shimsScript);
                
                _logger.LogDebug("Anti-fingerprinting shims injected");
            }
            else
            {
                _logger.LogWarning("Anti-fingerprinting shims file not found: {Path}", shimsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject privacy shims");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            IsLoading = true;
            LoadingUrl = e.Uri;
            
            // Apply URL sanitization
            var result = _urlSanitizer.Sanitize(e.Uri);
            
            if (result.RemovedParams.Length > 0)
            {
                // Cancel current navigation and redirect to sanitized URL
                e.Cancel = true;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10); // Small delay to avoid recursion
                    await _coreWebView2!.NavigateAsync(result.SanitizedUrl);
                });
                
                _logger.LogInformation("URL sanitized during navigation: removed {Count} parameters", result.RemovedParams.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during navigation starting");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            IsLoading = false;
            LoadingUrl = "";
            
            if (e.IsSuccess)
            {
                SecurityStatus = "Secure";
                HasSecurityWarning = false;
            }
            else
            {
                SecurityStatus = "Load Failed";
                HasSecurityWarning = true;
                _logger.LogWarning("Navigation failed for URL: {Url}", Url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during navigation completed");
        }
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        try
        {
            if (_coreWebView2 != null)
            {
                Title = string.IsNullOrWhiteSpace(_coreWebView2.DocumentTitle) 
                    ? "Secure Container" 
                    : _coreWebView2.DocumentTitle;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document title");
        }
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        try
        {
            if (_coreWebView2 != null)
            {
                Url = _coreWebView2.Source;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating source URL");
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            if (_coreWebView2 != null)
            {
                await _coreWebView2.ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh page");
        }
    }

    private void GoBack()
    {
        try
        {
            _coreWebView2?.GoBack();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go back");
        }
    }

    private void GoForward()
    {
        try
        {
            _coreWebView2?.GoForward();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go forward");
        }
    }

    private bool CanGoBack()
    {
        return _coreWebView2?.CanGoBack == true;
    }

    private bool CanGoForward()
    {
        return _coreWebView2?.CanGoForward == true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _logger.LogInformation("Disposing TabViewModel for profile: {ProfileId}", _profileInfo.ProfileId);
            
            // Remove event handlers
            if (_coreWebView2 != null)
            {
                _coreWebView2.NavigationStarting -= OnNavigationStarting;
                _coreWebView2.NavigationCompleted -= OnNavigationCompleted;
                _coreWebView2.DocumentTitleChanged -= OnDocumentTitleChanged;
                _coreWebView2.SourceChanged -= OnSourceChanged;
            }
            
            // Dispose profile through profile manager
            await _profileManager.DisposeProfileAsync(_profileInfo.ProfileId);
            
            _logger.LogInformation("TabViewModel disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TabViewModel disposal");
        }
    }
}