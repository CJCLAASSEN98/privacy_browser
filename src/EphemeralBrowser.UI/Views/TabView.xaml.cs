using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using EphemeralBrowser.UI.ViewModels;

namespace EphemeralBrowser.UI.Views;

public partial class TabView : UserControl
{
    private TabViewModel? _viewModel;
    private bool _isInitialized;
    private readonly object _initializationLock = new();

    public TabView()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Clean up previous view model
        if (e.OldValue is TabViewModel oldViewModel && _viewModel == oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnTabPropertyChanged;
        }

        if (e.NewValue is TabViewModel newViewModel)
        {
            _viewModel = newViewModel;
            _viewModel.PropertyChanged += OnTabPropertyChanged;
            
            // Reset initialization state for new view model
            _isInitialized = false;
            
            if (IsLoaded)
            {
                await InitializeWebViewAsync();
            }
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && !_isInitialized)
        {
            await InitializeWebViewAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Don't dispose the TabViewModel here - it's managed by MainViewModel
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnTabPropertyChanged;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_viewModel == null || _isInitialized)
            return;

        lock (_initializationLock)
        {
            if (_isInitialized)
                return;
            _isInitialized = true;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Initializing TabView for profile: {_viewModel.ProfileId}");
            
            // Get the environment for this profile from the TabViewModel
            var environment = await _viewModel.GetEnvironmentAsync();
            if (environment != null)
            {
                // Initialize WebView2 with the correct ephemeral environment
                await WebView.EnsureCoreWebView2Async(environment);
            }
            else
            {
                // Fallback to default initialization
                await WebView.EnsureCoreWebView2Async();
            }
            
            System.Diagnostics.Debug.WriteLine("TabView WebView2 initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize TabView: {ex.Message}");
            _isInitialized = false; // Allow retry
        }
    }

    private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 core initialization completed - Success: {e.IsSuccess}");
            
            if (e.IsSuccess && _viewModel != null && WebView.CoreWebView2 != null)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 core initialization completed for profile: {_viewModel.ProfileId}");
                System.Diagnostics.Debug.WriteLine($"CoreWebView2 instance: {WebView.CoreWebView2.GetHashCode()}");
                
                // Connect the TabViewModel to the actual WebView2 instance
                await _viewModel.SetupWebView2Async(WebView.CoreWebView2);
                
                System.Diagnostics.Debug.WriteLine("TabViewModel successfully connected to WebView2");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 core initialization failed: {e.InitializationException?.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during WebView2 initialization: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Navigation starting to: {e.Uri}");
            
            // Update the view model's URL property
            if (_viewModel != null)
            {
                _viewModel.Url = e.Uri;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during navigation starting event: {ex.Message}");
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Navigation completed, success: {e.IsSuccess}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during navigation completed event: {ex.Message}");
        }
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Handle any property changes from the TabViewModel if needed
        // This helps keep the UI in sync with the view model
    }
}
