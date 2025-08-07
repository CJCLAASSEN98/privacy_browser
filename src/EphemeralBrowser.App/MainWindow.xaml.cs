using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using EphemeralBrowser.UI.ViewModels;

namespace EphemeralBrowser.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        DataContext = _viewModel;
        
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("MainWindow loading...");
            
            // Initialize the main view model
            await _viewModel.InitializeAsync();
            
            // Set focus to address bar for keyboard-first navigation
            AddressBar.Focus();
            
            _logger.LogInformation("MainWindow loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MainWindow");
            
            MessageBox.Show($"Failed to initialize browser: {ex.Message}", 
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            _logger.LogInformation("MainWindow closing...");
            
            // Clean up view model resources
            await _viewModel.CleanupAsync();
            
            _logger.LogInformation("MainWindow cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MainWindow cleanup");
            // Don't prevent close on cleanup errors
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Back button clicked directly (bypassing command system)");
            
            if (_viewModel.ActiveTab != null)
            {
                _logger.LogInformation("ActiveTab found - ProfileId: {ProfileId}, CanGoBack: {CanGoBack}", 
                    _viewModel.ActiveTab.ProfileId, _viewModel.ActiveTab.CanGoBack());
                    
                _viewModel.ActiveTab.GoBack();
            }
            else
            {
                _logger.LogWarning("No active tab when back button clicked");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in back button click handler");
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Forward button clicked directly (bypassing command system)");
            
            if (_viewModel.ActiveTab != null)
            {
                _logger.LogInformation("ActiveTab found - ProfileId: {ProfileId}, CanGoForward: {CanGoForward}", 
                    _viewModel.ActiveTab.ProfileId, _viewModel.ActiveTab.CanGoForward());
                    
                _viewModel.ActiveTab.GoForward();
            }
            else
            {
                _logger.LogWarning("No active tab when forward button clicked");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in forward button click handler");
        }
    }
}
