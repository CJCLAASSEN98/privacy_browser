using Microsoft.Web.WebView2.Core;

namespace EphemeralBrowser.Core.Services;

public readonly record struct DownloadInfo(
    string DownloadId,
    string FileName,
    string SourceUrl,
    string LocalPath,
    string QuarantinePath,
    string ContentType,
    long Size,
    string Sha256Hash,
    DateTime StartTime,
    DownloadStatus Status);

public enum DownloadStatus
{
    Pending,
    InProgress,
    Quarantined,
    Promoted,
    Deleted,
    Failed
}

public readonly record struct DownloadMetrics(
    int TotalDownloads,
    int QuarantinedDownloads,
    int PromotedDownloads,
    int DeletedDownloads,
    long TotalBytesDownloaded,
    TimeSpan AverageDownloadTime);

public interface IDownloadGate
{
    event EventHandler<DownloadInfo> DownloadCompleted;
    event EventHandler<DownloadInfo> DownloadPromoted;
    event EventHandler<DownloadInfo> DownloadDeleted;
    
    Task InitializeAsync(CoreWebView2 webView, string quarantineDirectory);
    Task<DownloadInfo> PromoteDownloadAsync(string downloadId, string destinationPath);
    Task<bool> DeleteDownloadAsync(string downloadId);
    DownloadInfo[] GetActiveDownloads();
    DownloadInfo? GetDownloadInfo(string downloadId);
    DownloadMetrics GetMetrics();
}