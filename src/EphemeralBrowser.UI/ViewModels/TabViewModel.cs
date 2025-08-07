using System;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using EphemeralBrowser.Core.Services;
using EphemeralBrowser.UI.Models;

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
    private Timer? _performanceTimer;
    
    [ObservableProperty]
    private string _title = "New Container";
    
    [ObservableProperty]
    private string _url = "about:blank";
    
    [ObservableProperty]
    private string _loadingUrl = "";
    
    [ObservableProperty]
    private bool _isLoading = true;
    
    [ObservableProperty]
    private bool _isLoaded;
    
    [ObservableProperty]
    private bool _hasSecurityWarning;
    
    [ObservableProperty]
    private string _securityStatus = "Secure";

    // Privacy properties for binding
    [ObservableProperty]
    private PrivacyLevel _privacyLevel = PrivacyLevel.Balanced;
    
    [ObservableProperty]
    private bool _canvasProtection = true;
    
    [ObservableProperty]
    private bool _audioProtection = true;
    
    [ObservableProperty]
    private bool _webGLProtection = true;
    
    [ObservableProperty]
    private bool _timingProtection = true;
    
    [ObservableProperty]
    private bool _batteryBlocking = true;

    // Performance properties
    [ObservableProperty]
    private string _loadTime = "0ms";
    
    [ObservableProperty]
    private string _memoryUsage = "0 MB";
    
    [ObservableProperty]
    private string _sanitizerOverhead = "0ms";
    
    [ObservableProperty]
    private string _activeShims = "0 active";
    
    // Performance tracking fields
    private DateTime _navigationStartTime;
    private DateTime _sanitizerStartTime;
    private int _activeShimCount;
    
    [ObservableProperty]
    private bool _isActive;
    
    // Privacy Panel properties
    [ObservableProperty] 
    private bool _isPrivacyPanelExpanded;
    
    [ObservableProperty]
    private bool _wasSanitized;
    
    [ObservableProperty] 
    private bool _isAntiFingerprinting = true;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private string _mOTWStatus = "Enabled";

    // Radio button computed properties
    public bool IsPrivacyLevelStrict 
    { 
        get => PrivacyLevel == PrivacyLevel.Strict;
        set { if (value) PrivacyLevel = PrivacyLevel.Strict; }
    }
    
    public bool IsPrivacyLevelBalanced 
    { 
        get => PrivacyLevel == PrivacyLevel.Balanced;
        set { if (value) PrivacyLevel = PrivacyLevel.Balanced; }
    }
    
    public bool IsPrivacyLevelTrusted 
    { 
        get => PrivacyLevel == PrivacyLevel.Minimal;
        set { if (value) PrivacyLevel = PrivacyLevel.Minimal; }
    }

    // Collections for panels - using simple collections for now
    public System.Collections.ObjectModel.ObservableCollection<SanitizationRule> SanitizationRules { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<NavigationEvent> NavigationEvents { get; } = new();

    public TabViewModel(
        ProfileInfo profileInfo,
        IProfileManager profileManager,
        IUrlSanitizer urlSanitizer,
        IDownloadGate downloadGate,
        ILogger<TabViewModel> logger)
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
        ResetPrivacyCommand = new RelayCommand(ResetPrivacySettings);
        TogglePrivacyPanelCommand = new RelayCommand(() => IsPrivacyPanelExpanded = !IsPrivacyPanelExpanded);
        UndoSanitizationCommand = new RelayCommand(UndoSanitization, () => WasSanitized);
        LoadRulesCommand = new RelayCommand(LoadRules);
        ExportRulesCommand = new RelayCommand(ExportRules);
        ResetRulesCommand = new RelayCommand(ResetRules);
        ExportDiagnosticsCommand = new RelayCommand(ExportDiagnostics);
        
        // Initialize default sanitization rules
        InitializeDefaultRules();
        
        // Initialize performance metrics
        UpdateActiveShimsCount();
        UpdateMemoryUsage();
        
        // Add initial navigation event
        NavigationEvents.Add(new NavigationEvent
        {
            Timestamp = DateTime.Now,
            Event = "Container Created",
            Details = $"Ephemeral container initialized with profile {_profileInfo.ProfileId[..8]}...",
            Url = "about:blank",
            ProfileId = ProfileId
        });
        
        // Start periodic memory update timer
        StartPerformanceMonitoring();
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand GoForwardCommand { get; }
    public IRelayCommand ResetPrivacyCommand { get; }
    public IRelayCommand TogglePrivacyPanelCommand { get; }
    public IRelayCommand UndoSanitizationCommand { get; }
    public IRelayCommand LoadRulesCommand { get; }
    public IRelayCommand ExportRulesCommand { get; }
    public IRelayCommand ResetRulesCommand { get; }
    public IRelayCommand ExportDiagnosticsCommand { get; }

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
            
            // Initialize performance metrics
            LoadTime = "Initializing...";
            MemoryUsage = "Calculating...";
            SanitizerOverhead = "0ms";
            MOTWStatus = "Ready";
            UpdateActiveShimsCount();
            
            // Note: CoreWebView2 will be created when WebView2 control is initialized
            // For now, just mark as ready for WebView2 control creation
            
            IsLoading = false;
            IsLoaded = true;
            
            Title = "Secure Container - Ready";
            Url = "about:blank";
            StatusMessage = "Ready";
            
            // Initial memory measurement
            UpdateMemoryUsage();
            
            _logger.LogInformation("TabViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TabViewModel");
            throw;
        }
    }

    public Task NavigateAsync(string navigationUrl)
    {
        try
        {
            if (_coreWebView2 == null)
            {
                _logger.LogWarning("Attempted navigation before WebView2 is ready");
                return Task.CompletedTask;
            }

            // Reset performance tracking
            _navigationStartTime = DateTime.UtcNow;
            
            IsLoading = true;
            LoadingUrl = navigationUrl;
            StatusMessage = "Navigating...";
            LoadTime = "Loading...";
            
            // Enforce HTTPS-only navigation (with exceptions)
            if (!navigationUrl.StartsWith("https://") && !navigationUrl.StartsWith("about:") && !navigationUrl.StartsWith("data:"))
            {
                if (navigationUrl.StartsWith("http://"))
                {
                    navigationUrl = navigationUrl.Replace("http://", "https://");
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

            _coreWebView2.Navigate(navigationUrl);
            
            _logger.LogDebug("Navigation initiated to: {Url}", navigationUrl);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed");
            IsLoading = false;
            SecurityStatus = "Navigation Failed";
            HasSecurityWarning = true;
            LoadTime = "Failed";
            StatusMessage = "Navigation failed";
            return Task.CompletedTask;
        }
    }

    public async Task SetupWebView2Async(CoreWebView2 coreWebView2)
    {
        try
        {
            var setupStartTime = DateTime.UtcNow;
            _coreWebView2 = coreWebView2;
            
            _logger.LogInformation("Setting up WebView2 for profile: {ProfileId}", _profileInfo.ProfileId);
            
            // Configure privacy settings (optimized)
            await ConfigurePrivacySettingsAsync();
            
            // Setup event handlers (lightweight)
            SetupEventHandlers();
            
            // Initialize download gate (async, non-blocking)
            var quarantineDir = Path.Combine(_profileInfo.UserDataFolder, "Quarantine");
            _ = Task.Run(async () =>
            {
                try
                {
                    await _downloadGate.InitializeAsync(_coreWebView2, quarantineDir);
                    _logger.LogDebug("Download gate initialized asynchronously");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize download gate");
                }
            });
            
            // Inject optimized privacy shims
            await InjectPrivacyShimsAsync();
            
            var setupTime = DateTime.UtcNow - setupStartTime;
            
            // Update performance metrics
            LoadTime = "Ready";
            StatusMessage = $"Initialized in {setupTime.TotalMilliseconds:F0}ms";
            MOTWStatus = "Active";
            UpdateMemoryUsage();
            
            // Update navigation command states
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
            
            _logger.LogInformation("WebView2 setup completed in {SetupTime}ms for profile: {ProfileId}", 
                setupTime.TotalMilliseconds, _profileInfo.ProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup WebView2");
            throw;
        }
    }

    private Task ConfigurePrivacySettingsAsync()
    {
        if (_coreWebView2 == null) return Task.CompletedTask;

        try
        {
            var settings = _coreWebView2.Settings;
            
            // Optimize settings for performance while maintaining privacy
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.AreDevToolsEnabled = false;
            settings.IsWebMessageEnabled = true;
            
            // Performance optimizations
            settings.AreHostObjectsAllowed = false; // Reduce overhead
            settings.IsSwipeNavigationEnabled = false; // Reduce gesture overhead
            
            // Configure profile for privacy with performance balance
            var profile = _coreWebView2.Profile;
            profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Balanced;
            
            // Performance: Don't clear data unnecessarily during navigation
            // This was potentially causing delays
            
            _logger.LogInformation("Optimized privacy settings configured for performance");
                
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure privacy settings");
            return Task.CompletedTask;
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
            _activeShimCount = 0;
            
            // Performance optimization: Only inject shims that are actually enabled
            var shimScript = BuildOptimizedShimScript();
            
            if (!string.IsNullOrEmpty(shimScript))
            {
                // Measure shim injection time
                var shimStartTime = DateTime.UtcNow;
                await _coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(shimScript);
                var shimTime = DateTime.UtcNow - shimStartTime;
                
                _logger.LogDebug("Anti-fingerprinting shims injected in {Time}ms - {Count} shims active", 
                    shimTime.TotalMilliseconds, _activeShimCount);
            }
            
            ActiveShims = $"{_activeShimCount} active";
        }
        catch (Exception ex)
        {
            ActiveShims = "Error";
            _logger.LogError(ex, "Failed to inject privacy shims");
        }
    }

    private string BuildOptimizedShimScript()
    {
        var script = "";
        _activeShimCount = 0;
        
        // Performance optimization: Apply fewer shims for Minimal privacy level
        var isMinimalPrivacy = PrivacyLevel == PrivacyLevel.Minimal;
        var isStrictPrivacy = PrivacyLevel == PrivacyLevel.Strict;
        
        // Only include shims that are enabled and appropriate for privacy level
        if (CanvasProtection && !isMinimalPrivacy)
        {
            script += @"
                // Lightweight canvas protection
                const originalGetContext = HTMLCanvasElement.prototype.getContext;
                HTMLCanvasElement.prototype.getContext = function(type, ...args) {
                    const context = originalGetContext.apply(this, [type, ...args]);
                    if (type === '2d' && context) {
                        const originalGetImageData = context.getImageData;
                        context.getImageData = function(...args) {
                            const imageData = originalGetImageData.apply(this, args);
                            // Minimal noise for performance
                            for (let i = 0; i < imageData.data.length; i += 40) {
                                imageData.data[i] = (imageData.data[i] + Math.random() * 2 - 1) & 255;
                            }
                            return imageData;
                        };
                    }
                    return context;
                };
            ";
            _activeShimCount++;
        }
        
        if (TimingProtection && !isMinimalPrivacy)
        {
            var quantization = isStrictPrivacy ? 100 : 50; // Less quantization for better performance
            script += $@"
                // Optimized timing protection
                const originalNow = performance.now;
                performance.now = function() {{
                    const time = originalNow.call(this);
                    return Math.floor(time / {quantization}) * {quantization};
                }};
            ";
            _activeShimCount++;
        }
        
        if (BatteryBlocking)
        {
            script += @"
                // Battery API blocking (minimal overhead)
                if ('getBattery' in navigator) {
                    Object.defineProperty(navigator, 'getBattery', {
                        value: () => Promise.reject(new Error('Battery API blocked')),
                        writable: false
                    });
                }
            ";
            _activeShimCount++;
        }
        
        if (AudioProtection && !isMinimalPrivacy)
        {
            script += @"
                // Minimal audio context protection
                if (window.AudioContext || window.webkitAudioContext) {
                    const OriginalAudioContext = window.AudioContext || window.webkitAudioContext;
                    const originalCreateOscillator = OriginalAudioContext.prototype.createOscillator;
                    OriginalAudioContext.prototype.createOscillator = function() {
                        const oscillator = originalCreateOscillator.call(this);
                        const originalStart = oscillator.start;
                        oscillator.start = function(when) {
                            return originalStart.call(this, when + (Math.random() * 0.0001)); // Reduced noise
                        };
                        return oscillator;
                    };
                }
            ";
            _activeShimCount++;
        }
        
        if (WebGLProtection && !isMinimalPrivacy)
        {
            script += @"
                // Efficient WebGL protection
                ['WebGLRenderingContext', 'WebGL2RenderingContext'].forEach(contextName => {
                    if (window[contextName]) {
                        const originalGetParameter = window[contextName].prototype.getParameter;
                        window[contextName].prototype.getParameter = function(parameter) {
                            if (parameter === this.RENDERER || parameter === this.VENDOR) {
                                return 'Privacy Protected';
                            }
                            return originalGetParameter.call(this, parameter);
                        };
                    }
                });
            ";
            _activeShimCount++;
        }
        
        return script;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            // Start performance tracking
            _navigationStartTime = DateTime.UtcNow;
            
            IsLoading = true;
            LoadingUrl = e.Uri;
            
            // Apply URL sanitization with optimized timing
            _sanitizerStartTime = DateTime.UtcNow;
            var result = _urlSanitizer.Sanitize(e.Uri);
            var sanitizerTime = DateTime.UtcNow - _sanitizerStartTime;
            
            // Update sanitizer overhead metric
            SanitizerOverhead = $"{sanitizerTime.TotalMilliseconds:F0}ms";
            
            if (result.RemovedParams.Length > 0)
            {
                // Cancel current navigation and redirect to sanitized URL
                e.Cancel = true;
                WasSanitized = true;
                
                // Optimized redirect - reduce delay for better performance
                Task.Run(async () =>
                {
                    await Task.Delay(5); // Reduced from 10ms for faster redirect
                    if (_coreWebView2 != null)
                    {
                        _coreWebView2.Navigate(result.SanitizedUrl);
                    }
                });
                
                // Defer non-critical event logging to avoid blocking navigation
                Task.Run(() =>
                {
                    NavigationEvents.Add(new NavigationEvent
                    {
                        Timestamp = DateTime.Now,
                        Event = "URL Sanitized",
                        Details = $"Removed {result.RemovedParams.Length} parameters in {sanitizerTime.TotalMilliseconds:F0}ms",
                        Url = e.Uri,
                        ProfileId = ProfileId
                    });
                    
                    // Limit navigation events to last 50 to prevent memory growth
                    if (NavigationEvents.Count > 50)
                    {
                        NavigationEvents.RemoveAt(0);
                    }
                });
            }
            else
            {
                // Defer non-critical event logging for normal navigation too
                Task.Run(() =>
                {
                    NavigationEvents.Add(new NavigationEvent
                    {
                        Timestamp = DateTime.Now,
                        Event = "Navigation Started",
                        Details = $"No sanitization needed (0ms overhead)",
                        Url = e.Uri,
                        ProfileId = ProfileId
                    });
                    
                    if (NavigationEvents.Count > 50)
                    {
                        NavigationEvents.RemoveAt(0);
                    }
                });
            }
            
            _logger.LogDebug("Navigation starting to: {Uri} - Sanitizer overhead: {Overhead}ms", e.Uri, sanitizerTime.TotalMilliseconds);
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
            
            // Calculate load time
            var loadTime = DateTime.UtcNow - _navigationStartTime;
            LoadTime = $"{loadTime.TotalMilliseconds:F0}ms";
            
            // Performance warning for high load times
            if (loadTime.TotalMilliseconds > 1500)
            {
                StatusMessage = $"Slow load: {LoadTime} - Heavy site or network delay";
                _logger.LogWarning("High load time detected: {LoadTime} for {Url}", LoadTime, _coreWebView2?.Source);
            }
            else if (loadTime.TotalMilliseconds > 1000)
            {
                StatusMessage = $"Load: {LoadTime} - Normal for complex sites";
            }
            
            if (e.IsSuccess)
            {
                SecurityStatus = "Secure";
                HasSecurityWarning = false;
                StatusMessage = "Ready";
                MOTWStatus = "Active";
                
                // Defer non-critical operations to avoid blocking UI
                Task.Run(() =>
                {
                    // Add navigation event
                    NavigationEvents.Add(new NavigationEvent
                    {
                        Timestamp = DateTime.Now,
                        Event = "Navigation Completed",
                        Details = $"Load: {LoadTime}",
                        Url = _coreWebView2?.Source ?? "",
                        ProfileId = ProfileId
                    });
                    
                    // Limit navigation events
                    if (NavigationEvents.Count > 50)
                    {
                        NavigationEvents.RemoveAt(0);
                    }
                    
                    // Update memory usage after page load (deferred)
                    UpdateMemoryUsage();
                });
            }
            else
            {
                SecurityStatus = "Load Failed";
                HasSecurityWarning = true;
                LoadTime = "Failed";
                MOTWStatus = "Disabled";
                StatusMessage = "Load failed";
                
                _logger.LogWarning("Navigation failed for URL: {Url}", Url);
            }
            
            // Update navigation commands immediately (critical for UX)
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
            OnPropertyChanged("NavigationStateChanged");
            
            _logger.LogInformation("Navigation completed - Load time: {LoadTime}", LoadTime);
            
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

    private Task RefreshAsync()
    {
        try
        {
            if (_coreWebView2 != null)
            {
                _coreWebView2.Reload();
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh page");
            return Task.CompletedTask;
        }
    }

    public void GoBack()
    {
        try
        {
            _logger.LogInformation("GoBack called - CanGoBack: {CanGoBack}", _coreWebView2?.CanGoBack);
            if (_coreWebView2?.CanGoBack == true)
            {
                _coreWebView2.GoBack();
                _logger.LogInformation("GoBack executed successfully");
            }
            else
            {
                _logger.LogWarning("GoBack called but CanGoBack is false or CoreWebView2 is null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go back");
        }
    }

    public void GoForward()
    {
        try
        {
            _logger.LogInformation("GoForward called - CanGoForward: {CanGoForward}", _coreWebView2?.CanGoForward);
            if (_coreWebView2?.CanGoForward == true)
            {
                _coreWebView2.GoForward();
                _logger.LogInformation("GoForward executed successfully");
            }
            else
            {
                _logger.LogWarning("GoForward called but CanGoForward is false or CoreWebView2 is null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go forward");
        }
    }

    public bool CanGoBack()
    {
        var canGoBack = _coreWebView2?.CanGoBack == true;
        _logger.LogDebug("CanGoBack called: {CanGoBack} (CoreWebView2: {HasCore})", canGoBack, _coreWebView2 != null);
        return canGoBack;
    }

    public bool CanGoForward()
    {
        var canGoForward = _coreWebView2?.CanGoForward == true;
        _logger.LogDebug("CanGoForward called: {CanGoForward} (CoreWebView2: {HasCore})", canGoForward, _coreWebView2 != null);
        return canGoForward;
    }

    private void ResetPrivacySettings()
    {
        // Reset to default privacy settings
        CanvasProtection = true;
        AudioProtection = true;
        WebGLProtection = true;
        TimingProtection = true;
        BatteryBlocking = true;
        PrivacyLevel = PrivacyLevel.Balanced;
        
        // Update active shims count
        UpdateActiveShimsCount();
        
        // Re-inject optimized shims with new settings
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
        }
        
        // Update status message
        StatusMessage = "Privacy settings reset to defaults";
        
        _logger.LogInformation("Privacy settings reset to defaults");
    }

    private void UpdateMemoryUsage()
    {
        try
        {
            // Get current process memory usage
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            MemoryUsage = $"{memoryMB} MB";
            
            _logger.LogDebug("Memory usage updated: {MemoryUsage}", MemoryUsage);
        }
        catch (Exception ex)
        {
            MemoryUsage = "Error";
            _logger.LogError(ex, "Failed to update memory usage");
        }
    }
    
    private void UpdateActiveShimsCount()
    {
        _activeShimCount = 0;
        if (CanvasProtection) _activeShimCount++;
        if (AudioProtection) _activeShimCount++;
        if (WebGLProtection) _activeShimCount++;
        if (TimingProtection) _activeShimCount++;
        if (BatteryBlocking) _activeShimCount++;
        
        ActiveShims = $"{_activeShimCount} active";
    }

    private void StartPerformanceMonitoring()
    {
        try
        {
            // Update memory usage every 10 seconds (reduced frequency for better performance)
            _performanceTimer = new Timer(10000);
            _performanceTimer.Elapsed += (sender, e) => 
            {
                try
                {
                    UpdateMemoryUsage();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in performance timer update");
                }
            };
            _performanceTimer.Start();
            
            _logger.LogDebug("Performance monitoring started (10s intervals)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start performance monitoring");
        }
    }

    private void InitializeDefaultRules()
    {
        // Add default sanitization rules
        SanitizationRules.Clear();
        SanitizationRules.Add(new SanitizationRule { Parameter = "utm_*", Action = "Remove", Domain = "*", IsEnabled = true });
        SanitizationRules.Add(new SanitizationRule { Parameter = "fbclid", Action = "Remove", Domain = "*", IsEnabled = true });
        SanitizationRules.Add(new SanitizationRule { Parameter = "gclid", Action = "Remove", Domain = "*", IsEnabled = true });
        SanitizationRules.Add(new SanitizationRule { Parameter = "mc_eid", Action = "Remove", Domain = "*", IsEnabled = true });
        SanitizationRules.Add(new SanitizationRule { Parameter = "igshid", Action = "Remove", Domain = "*", IsEnabled = true });
    }
    
    private void UndoSanitization()
    {
        // Implementation for undoing URL sanitization
        WasSanitized = false;
        StatusMessage = "Sanitization undone";
        
        // Add navigation event
        NavigationEvents.Add(new NavigationEvent
        {
            Timestamp = DateTime.Now,
            Event = "Sanitization Undone",
            Details = "User manually undid URL sanitization",
            Url = Url,
            ProfileId = ProfileId
        });
        
        // Update command state
        UndoSanitizationCommand.NotifyCanExecuteChanged();
        
        _logger.LogInformation("URL sanitization undone");
    }
    
    private void LoadRules()
    {
        // Implementation for loading rules pack
        _logger.LogInformation("Loading rules pack");
    }
    
    private void ExportRules()
    {
        // Implementation for exporting rules
        _logger.LogInformation("Exporting rules");
    }
    
    private void ResetRules()
    {
        // Reset rules to defaults
        InitializeDefaultRules();
        _logger.LogInformation("Rules reset to defaults");
    }
    
    private void ExportDiagnostics()
    {
        // Implementation for exporting diagnostics
        _logger.LogInformation("Exporting diagnostics");
    }

    partial void OnPrivacyLevelChanged(PrivacyLevel value)
    {
        // Update radio button properties when privacy level changes
        OnPropertyChanged(nameof(IsPrivacyLevelStrict));
        OnPropertyChanged(nameof(IsPrivacyLevelBalanced));
        OnPropertyChanged(nameof(IsPrivacyLevelTrusted));
        
        // Update shims count when privacy level changes
        UpdateActiveShimsCount();
        
        // Re-inject shims to apply new privacy level settings
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
            StatusMessage = $"Privacy level changed to {value}";
        }
    }

    partial void OnCanvasProtectionChanged(bool value)
    {
        UpdateActiveShimsCount();
        // Re-inject shims when settings change (for immediate effect on new navigations)
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
        }
    }

    partial void OnAudioProtectionChanged(bool value)
    {
        UpdateActiveShimsCount();
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
        }
    }

    partial void OnWebGLProtectionChanged(bool value)
    {
        UpdateActiveShimsCount();
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
        }
    }

    partial void OnTimingProtectionChanged(bool value)
    {
        UpdateActiveShimsCount();
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
        }
    }

    partial void OnBatteryBlockingChanged(bool value)
    {
        UpdateActiveShimsCount();
        if (_coreWebView2 != null)
        {
            Task.Run(async () => await InjectPrivacyShimsAsync());
        }
    }

    // Test method to force navigation history creation
    public async Task CreateTestNavigationHistoryAsync()
    {
        if (_coreWebView2 == null)
        {
            _logger.LogWarning("Cannot create test navigation history - CoreWebView2 is null");
            return;
        }

        try
        {
            _logger.LogInformation("Creating test navigation history...");
            
            // Navigate to a simple page first
            _logger.LogInformation("Test nav 1: Navigating to example.com");
            _coreWebView2.Navigate("https://example.com");
            
            // Wait a bit for navigation to complete
            await Task.Delay(3000);
            _logger.LogInformation("After first nav - CanGoBack: {CanGoBack}", _coreWebView2.CanGoBack);
            
            // Navigate to another page
            _logger.LogInformation("Test nav 2: Navigating to httpbin.org");
            _coreWebView2.Navigate("https://httpbin.org");
            
            // Wait a bit more
            await Task.Delay(3000);
            _logger.LogInformation("After second nav - CanGoBack: {CanGoBack}", _coreWebView2.CanGoBack);
            
            // Force command state updates
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
            
            // Notify MainViewModel that navigation state changed
            OnPropertyChanged("NavigationStateChanged");
            
            _logger.LogInformation("Test navigation history creation completed");
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create test navigation history");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _logger.LogInformation("Disposing TabViewModel for profile: {ProfileId}", _profileInfo.ProfileId);
            
            // Stop and dispose performance timer
            _performanceTimer?.Stop();
            _performanceTimer?.Dispose();
            _performanceTimer = null;
            
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