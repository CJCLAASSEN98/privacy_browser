using System.Windows;
using Microsoft.Extensions.Logging;
using EphemeralBrowser.UI.ViewModels;
using Microsoft.Web.WebView2.Core;
using System.IO;

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

    private async void OnLoaded(object sender, RoutedEventArgs e)
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

    private async void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _logger.LogInformation("MainWindow closing...");
            
            // Clean up view model resources
            if (_viewModel != null)
            {
                await _viewModel.CleanupAsync();
            }
            
            _logger.LogInformation("MainWindow cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MainWindow cleanup");
            // Don't prevent close on cleanup errors
        }
    }

    private void AddressBar_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            _viewModel.NavigateCommand.Execute(AddressBar.Text);
        }
    }
}