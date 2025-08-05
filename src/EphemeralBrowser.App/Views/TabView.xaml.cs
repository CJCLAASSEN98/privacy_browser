using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using EphemeralBrowser.UI.ViewModels;

namespace EphemeralBrowser.App.Views;

public partial class TabView : UserControl
{
    private TabViewModel? _viewModel;
    private bool _isInitialized;

    public TabView()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is TabViewModel newViewModel)
        {
            _viewModel = newViewModel;
            
            if (IsLoaded && !_isInitialized)
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

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel != null)
            {
                await _viewModel.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during TabView unload: {ex.Message}");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_viewModel == null || _isInitialized)
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine($"Initializing TabView for profile: {_viewModel.ProfileId}");
            
            // Initialize the TabViewModel to get the WebView2 environment
            await _viewModel.InitializeAsync();
            
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
            
            _isInitialized = true;
            
            System.Diagnostics.Debug.WriteLine("TabView initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize TabView: {ex.Message}");
        }
    }

    private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        try
        {
            if (e.IsSuccess && _viewModel != null && WebView.CoreWebView2 != null)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 core initialization completed for profile: {_viewModel.ProfileId}");
                
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
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Navigation starting to: {e.Uri}");
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
}