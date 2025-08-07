using CommunityToolkit.Mvvm.ComponentModel;

namespace EphemeralBrowser.UI.Models;

public partial class SanitizationRule : ObservableObject
{
    [ObservableProperty]
    private string _parameter = "";
    
    [ObservableProperty]
    private string _action = "Remove";
    
    [ObservableProperty]
    private string _domain = "*";
    
    [ObservableProperty]
    private bool _isEnabled = true;
    
    [ObservableProperty]
    private string _description = "";
}
