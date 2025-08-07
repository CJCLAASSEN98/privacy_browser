using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EphemeralBrowser.UI.Models;

public partial class NavigationEvent : ObservableObject
{
    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;
    
    [ObservableProperty]
    private string _event = "";
    
    [ObservableProperty]
    private string _details = "";
    
    [ObservableProperty]
    private string _url = "";
    
    [ObservableProperty]
    private string _profileId = "";
}
