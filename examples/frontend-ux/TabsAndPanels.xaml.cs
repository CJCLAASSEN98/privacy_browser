using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EphemeralBrowser.Views;

public partial class TabsAndPanels : UserControl
{
    public TabsAndPanels()
    {
        InitializeComponent();
        DataContext = new TabsAndPanelsViewModel();
    }
}

namespace EphemeralBrowser.ViewModels;

public enum PrivacyLevel
{
    Strict,
    Balanced,
    Trusted
}

public enum PrivacyGrade
{
    A, B, C, D, F
}

public partial class TabsAndPanelsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TabViewModel> tabs = new();
    
    [ObservableProperty]
    private TabViewModel? activeTab;
    
    [ObservableProperty]
    private bool isPanelExpanded;
    
    [ObservableProperty]
    private string statusMessage = string.Empty;
    
    [ObservableProperty]
    private bool hasStatusMessage;
    
    [ObservableProperty]
    private bool hasActiveDownloads;
    
    [ObservableProperty]
    private string activeDownloadsText = string.Empty;
    
    [ObservableProperty]
    private string performanceInfo = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<SanitizationRule> sanitizationRules = new();
    
    [ObservableProperty]
    private ObservableCollection<NavigationEvent> navigationEvents = new();

    public TabsAndPanelsViewModel()
    {
        NewTabCommand = new RelayCommand(CreateNewTab);
        LoadRulesCommand = new AsyncRelayCommand(LoadRulesAsync);
        ExportRulesCommand = new AsyncRelayCommand(ExportRulesAsync);
        ResetRulesCommand = new RelayCommand(ResetRules);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync);
        
        InitializeDefaultRules();
        CreateNewTab(); // Create first tab
        
        // Update performance info periodically
        var timer = new System.Timers.Timer(1000);
        timer.Elapsed += UpdatePerformanceInfo;
        timer.Start();
    }

    public ICommand NewTabCommand { get; }
    public IAsyncRelayCommand LoadRulesCommand { get; }
    public IAsyncRelayCommand ExportRulesCommand { get; }
    public ICommand ResetRulesCommand { get; }
    public IAsyncRelayCommand ExportDiagnosticsCommand { get; }

    private void CreateNewTab()
    {
        var newTab = new TabViewModel($"New Container {Tabs.Count + 1}");
        newTab.PropertyChanged += OnTabPropertyChanged;
        
        Tabs.Add(newTab);
        ActiveTab = newTab;
        
        AddNavigationEvent("Tab Created", $"Container {newTab.Title} created");
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TabViewModel tab && tab == ActiveTab)
        {
            switch (e.PropertyName)
            {
                case nameof(TabViewModel.WasSanitized):
                    OnPropertyChanged(nameof(HasStatusMessage));
                    break;
                case nameof(TabViewModel.IsLoading):
                    UpdateStatusMessage();
                    break;
            }
        }
    }

    private void UpdateStatusMessage()
    {
        if (ActiveTab?.IsLoading == true)
        {
            StatusMessage = $"Loading {ActiveTab.Url}...";
            HasStatusMessage = true;
        }
        else
        {
            StatusMessage = string.Empty;
            HasStatusMessage = ActiveTab?.WasSanitized == true || HasActiveDownloads;
        }
    }

    private void UpdatePerformanceInfo(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var tabCount = Tabs.Count;
            PerformanceInfo = $"Memory: {memoryMB:F1} MB | Containers: {tabCount}";
        });
    }

    private void InitializeDefaultRules()
    {
        var defaultRules = new[]
        {
            new SanitizationRule { Parameter = "utm_source", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "utm_medium", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "utm_campaign", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "fbclid", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "gclid", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "mc_eid", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "igshid", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "_ga", Action = "Remove", Domain = "*", IsEnabled = true },
            new SanitizationRule { Parameter = "msclkid", Action = "Remove", Domain = "*", IsEnabled = true }
        };

        foreach (var rule in defaultRules)
        {
            SanitizationRules.Add(rule);
        }
    }

    private async Task LoadRulesAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Load Sanitization Rules"
            };

            if (dialog.ShowDialog() == true)
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                // TODO: Deserialize and validate rules
                AddNavigationEvent("Rules Loaded", $"Loaded rules from {Path.GetFileName(dialog.FileName)}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load rules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExportRulesAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Export Sanitization Rules",
                FileName = "sanitization-rules.json"
            };

            if (dialog.ShowDialog() == true)
            {
                // TODO: Serialize rules to JSON
                await File.WriteAllTextAsync(dialog.FileName, "{}"); // Placeholder
                AddNavigationEvent("Rules Exported", $"Exported rules to {Path.GetFileName(dialog.FileName)}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export rules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetRules()
    {
        SanitizationRules.Clear();
        InitializeDefaultRules();
        AddNavigationEvent("Rules Reset", "Reset to default sanitization rules");
    }

    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Export Diagnostics",
                FileName = $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var diagnostics = new
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveTab = ActiveTab?.Title,
                    NavigationEvents = NavigationEvents,
                    PerformanceInfo,
                    TabCount = Tabs.Count
                };

                var json = System.Text.Json.JsonSerializer.Serialize(diagnostics, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dialog.FileName, json);
                
                AddNavigationEvent("Diagnostics Exported", $"Exported to {Path.GetFileName(dialog.FileName)}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export diagnostics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddNavigationEvent(string eventName, string details)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            NavigationEvents.Insert(0, new NavigationEvent
            {
                Timestamp = DateTime.Now,
                Event = eventName,
                Details = details
            });

            // Keep only last 100 events
            while (NavigationEvents.Count > 100)
            {
                NavigationEvents.RemoveAt(NavigationEvents.Count - 1);
            }
        });
    }
}

public partial class TabViewModel : ObservableObject
{
    [ObservableProperty]
    private string title;
    
    [ObservableProperty]
    private string url = "about:blank";
    
    [ObservableProperty]
    private bool isLoading;
    
    [ObservableProperty]
    private bool wasSanitized;
    
    [ObservableProperty]
    private PrivacyLevel privacyLevel = PrivacyLevel.Balanced;
    
    [ObservableProperty]
    private PrivacyGrade privacyGrade = PrivacyGrade.B;
    
    [ObservableProperty]
    private string privacyGradeTooltip = "Balanced privacy protection";
    
    [ObservableProperty]
    private bool canvasProtection = true;
    
    [ObservableProperty]
    private bool audioProtection = true;
    
    [ObservableProperty]
    private bool webGLProtection = true;
    
    [ObservableProperty]
    private bool timingProtection = true;
    
    [ObservableProperty]
    private bool batteryBlocking = true;
    
    [ObservableProperty]
    private bool antifingerprinting = true;
    
    [ObservableProperty]
    private string loadTime = "0ms";
    
    [ObservableProperty]
    private string memoryUsage = "0 MB";
    
    [ObservableProperty]
    private string sanitizerOverhead = "0ms";
    
    [ObservableProperty]
    private string activeShims = "5/5";
    
    [ObservableProperty]
    private string motwStatus = "Active";
    
    [ObservableProperty]
    private object? webViewContent;

    public TabViewModel(string title)
    {
        Title = title;
        CloseTabCommand = new RelayCommand(CloseTab);
        UndoSanitizationCommand = new RelayCommand(UndoSanitization);
        ResetPrivacyCommand = new RelayCommand(ResetPrivacySettings);
        
        UpdatePrivacyGrade();
    }

    public ICommand CloseTabCommand { get; }
    public ICommand UndoSanitizationCommand { get; }
    public ICommand ResetPrivacyCommand { get; }

    partial void OnPrivacyLevelChanged(PrivacyLevel value)
    {
        UpdatePrivacyGrade();
        UpdateProtectionSettings();
    }

    private void UpdatePrivacyGrade()
    {
        (PrivacyGrade, PrivacyGradeTooltip) = PrivacyLevel switch
        {
            PrivacyLevel.Strict => (PrivacyGrade.A, "Strict privacy protection - Maximum security"),
            PrivacyLevel.Balanced => (PrivacyGrade.B, "Balanced privacy protection - Recommended"),
            PrivacyLevel.Trusted => (PrivacyGrade.C, "Trusted site - Minimal protection"),
            _ => (PrivacyGrade.B, "Balanced privacy protection")
        };
    }

    private void UpdateProtectionSettings()
    {
        switch (PrivacyLevel)
        {
            case PrivacyLevel.Strict:
                CanvasProtection = true;
                AudioProtection = true;
                WebGLProtection = true;
                TimingProtection = true;
                BatteryBlocking = true;
                break;
            case PrivacyLevel.Balanced:
                CanvasProtection = true;
                AudioProtection = true;
                WebGLProtection = false;
                TimingProtection = true;
                BatteryBlocking = true;
                break;
            case PrivacyLevel.Trusted:
                CanvasProtection = false;
                AudioProtection = false;
                WebGLProtection = false;
                TimingProtection = false;
                BatteryBlocking = false;
                break;
        }
        
        UpdateActiveShims();
    }

    private void UpdateActiveShims()
    {
        int activeCount = 0;
        if (CanvasProtection) activeCount++;
        if (AudioProtection) activeCount++;
        if (WebGLProtection) activeCount++;
        if (TimingProtection) activeCount++;
        if (BatteryBlocking) activeCount++;
        
        ActiveShims = $"{activeCount}/5";
    }

    private void CloseTab()
    {
        // TODO: Implement tab closing logic
        // This would typically be handled by the parent view model
    }

    private void UndoSanitization()
    {
        // TODO: Navigate to original URL
        WasSanitized = false;
    }

    private void ResetPrivacySettings()
    {
        PrivacyLevel = PrivacyLevel.Balanced;
        // Protection settings will be updated automatically via OnPrivacyLevelChanged
    }
}

public class SanitizationRule : ObservableObject
{
    private string parameter = string.Empty;
    private string action = string.Empty;
    private string domain = string.Empty;
    private bool isEnabled = true;

    public string Parameter
    {
        get => parameter;
        set => SetProperty(ref parameter, value);
    }

    public string Action
    {
        get => action;
        set => SetProperty(ref action, value);
    }

    public string Domain
    {
        get => domain;
        set => SetProperty(ref domain, value);
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }
}

public class NavigationEvent
{
    public DateTime Timestamp { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

namespace EphemeralBrowser.Converters;

public class PrivacyGradeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is PrivacyGrade grade)
        {
            return grade switch
            {
                PrivacyGrade.A => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green),
                PrivacyGrade.B => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen),
                PrivacyGrade.C => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange),
                PrivacyGrade.D => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed),
                PrivacyGrade.F => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
            };
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool boolValue && boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool boolValue && !boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is Visibility visibility && visibility != Visibility.Visible;
    }
}

public class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return Binding.DoNothing;
    }
}