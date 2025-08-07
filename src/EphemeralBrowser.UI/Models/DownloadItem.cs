using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EphemeralBrowser.UI.Models;

public partial class DownloadItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = "";
    
    [ObservableProperty]
    private string _sourceUrl = "";
    
    [ObservableProperty]
    private long _size;
    
    [ObservableProperty]
    private string _contentType = "";
    
    [ObservableProperty]
    private string _sha256Hash = "";
    
    [ObservableProperty]
    private DateTime _downloadTime = DateTime.Now;
    
    [ObservableProperty]
    private string _status = "Quarantined";
}
