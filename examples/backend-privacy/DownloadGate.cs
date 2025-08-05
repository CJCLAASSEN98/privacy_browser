using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace EphemeralBrowser.Privacy;

[ComImport]
[Guid("73db1241-1e85-4581-8fef-650dd2fa63b6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAttachmentExecute
{
    void SetClientTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetClientGuid([In] ref Guid guid);
    void SetLocalPath([MarshalAs(UnmanagedType.LPWStr)] string pszLocalPath);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
    void SetSource([MarshalAs(UnmanagedType.LPWStr)] string pszSource);
    void SetReferrer([MarshalAs(UnmanagedType.LPWStr)] string pszReferrer);
    void CheckPolicy();
    void Prompt(IntPtr hwnd, uint attachment_prompt, out int paction);
    void Save();
    void Execute(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszVerb, out IntPtr phProcess);
    void SaveWithUI(IntPtr hwnd);
    void ClearClientState();
}

[ComImport]
[Guid("4125dd96-e03a-4103-8f70-e0597d803b9c")]
public class AttachmentServices
{
}

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

public sealed class DownloadGate : IDownloadGate, IDisposable
{
    private readonly ConcurrentDictionary<string, DownloadInfo> _downloads = new();
    private readonly string[] _allowedContentTypes =
    {
        "application/pdf",
        "text/plain",
        "text/csv",
        "application/json",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "application/zip",
        "application/x-zip-compressed"
    };
    
    private readonly string[] _blockedExtensions =
    {
        ".exe", ".msi", ".bat", ".cmd", ".com", ".scr",
        ".pif", ".vbs", ".js", ".jar", ".app", ".deb", ".rpm"
    };

    private CoreWebView2? _webView;
    private string _quarantineDirectory = string.Empty;
    private volatile bool _disposed;
    private int _totalDownloads;
    private int _quarantinedDownloads;
    private int _promotedDownloads;
    private int _deletedDownloads;
    private long _totalBytesDownloaded;
    private readonly List<TimeSpan> _downloadTimes = new();

    public event EventHandler<DownloadInfo>? DownloadCompleted;
    public event EventHandler<DownloadInfo>? DownloadPromoted;
    public event EventHandler<DownloadInfo>? DownloadDeleted;

    public async Task InitializeAsync(CoreWebView2 webView, string quarantineDirectory)
    {
        ObjectDisposedException.ThrowIfDisposed(_disposed, this);
        
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _quarantineDirectory = quarantineDirectory ?? throw new ArgumentNullException(nameof(quarantineDirectory));
        
        Directory.CreateDirectory(_quarantineDirectory);
        
        _webView.DownloadStarting += OnDownloadStarting;
        _webView.DOMContentLoaded += OnDOMContentLoaded;
    }

    public async Task<DownloadInfo> PromoteDownloadAsync(string downloadId, string destinationPath)
    {
        ObjectDisposedException.ThrowIfDisposed(_disposed, this);
        
        if (!_downloads.TryGetValue(downloadId, out var download))
            throw new ArgumentException($"Download {downloadId} not found", nameof(downloadId));

        if (download.Status != DownloadStatus.Quarantined)
            throw new InvalidOperationException($"Download {downloadId} is not in quarantined state");

        try
        {
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Atomic move operation
            File.Move(download.QuarantinePath, destinationPath, overwrite: true);
            
            var promotedDownload = download with 
            { 
                Status = DownloadStatus.Promoted,
                LocalPath = destinationPath
            };
            
            _downloads.TryUpdate(downloadId, promotedDownload, download);
            Interlocked.Increment(ref _promotedDownloads);
            
            DownloadPromoted?.Invoke(this, promotedDownload);
            return promotedDownload;
        }
        catch (Exception ex)
        {
            var failedDownload = download with { Status = DownloadStatus.Failed };
            _downloads.TryUpdate(downloadId, failedDownload, download);
            throw new InvalidOperationException($"Failed to promote download {downloadId}: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteDownloadAsync(string downloadId)
    {
        ObjectDisposedException.ThrowIfDisposed(_disposed, this);
        
        if (!_downloads.TryGetValue(downloadId, out var download))
            return false;

        try
        {
            if (File.Exists(download.QuarantinePath))
            {
                await SecureDeleteFileAsync(download.QuarantinePath);
            }
            
            var deletedDownload = download with { Status = DownloadStatus.Deleted };
            _downloads.TryUpdate(downloadId, deletedDownload, download);
            Interlocked.Increment(ref _deletedDownloads);
            
            DownloadDeleted?.Invoke(this, deletedDownload);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public DownloadInfo[] GetActiveDownloads()
    {
        return _downloads.Values
            .Where(d => d.Status is DownloadStatus.InProgress or DownloadStatus.Quarantined)
            .ToArray();
    }

    public DownloadInfo? GetDownloadInfo(string downloadId)
    {
        return _downloads.TryGetValue(downloadId, out var download) ? download : null;
    }

    public DownloadMetrics GetMetrics()
    {
        var avgDownloadTime = _downloadTimes.Count > 0 
            ? TimeSpan.FromMilliseconds(_downloadTimes.Average(t => t.TotalMilliseconds))
            : TimeSpan.Zero;

        return new DownloadMetrics(
            _totalDownloads,
            _quarantinedDownloads,
            _promotedDownloads,
            _deletedDownloads,
            _totalBytesDownloaded,
            avgDownloadTime);
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var downloadId = Guid.NewGuid().ToString("N");
        var fileName = Path.GetFileName(e.DownloadOperation.Uri) ?? $"download_{downloadId}";
        var quarantinePath = Path.Combine(_quarantineDirectory, $"{downloadId}_{fileName}");
        
        // Validate content type and extension
        var contentType = e.DownloadOperation.MimeType ?? "application/octet-stream";
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        if (!IsContentTypeAllowed(contentType) || IsExtensionBlocked(extension))
        {
            e.Cancel = true;
            return;
        }

        var download = new DownloadInfo(
            downloadId,
            fileName,
            e.DownloadOperation.Uri,
            string.Empty,
            quarantinePath,
            contentType,
            e.DownloadOperation.TotalBytesToReceive ?? -1,
            string.Empty,
            DateTime.UtcNow,
            DownloadStatus.Pending);

        _downloads.TryAdd(downloadId, download);
        
        e.DownloadOperation.StateChanged += (_, _) => OnDownloadStateChanged(downloadId, e.DownloadOperation);
        e.ResultFilePath = quarantinePath;
        
        Interlocked.Increment(ref _totalDownloads);
    }

    private async void OnDownloadStateChanged(string downloadId, CoreWebView2DownloadOperation downloadOperation)
    {
        if (!_downloads.TryGetValue(downloadId, out var download))
            return;

        switch (downloadOperation.State)
        {
            case CoreWebView2DownloadState.InProgress:
                var inProgressDownload = download with { Status = DownloadStatus.InProgress };
                _downloads.TryUpdate(downloadId, inProgressDownload, download);
                break;

            case CoreWebView2DownloadState.Completed:
                await HandleDownloadCompletedAsync(downloadId, downloadOperation, download);
                break;

            case CoreWebView2DownloadState.Interrupted:
                var failedDownload = download with { Status = DownloadStatus.Failed };
                _downloads.TryUpdate(downloadId, failedDownload, download);
                break;
        }
    }

    private async Task HandleDownloadCompletedAsync(string downloadId, CoreWebView2DownloadOperation downloadOperation, DownloadInfo download)
    {
        try
        {
            var downloadTime = DateTime.UtcNow - download.StartTime;
            lock (_downloadTimes) { _downloadTimes.Add(downloadTime); }
            
            var size = new FileInfo(download.QuarantinePath).Length;
            var hash = await ComputeSha256HashAsync(download.QuarantinePath);
            
            // Apply MOTW (Mark of the Web)
            await ApplyMarkOfTheWebAsync(download.QuarantinePath, download.SourceUrl);
            
            var completedDownload = download with 
            { 
                Status = DownloadStatus.Quarantined,
                Size = size,
                Sha256Hash = hash
            };
            
            _downloads.TryUpdate(downloadId, completedDownload, download);
            Interlocked.Increment(ref _quarantinedDownloads);
            Interlocked.Add(ref _totalBytesDownloaded, size);
            
            DownloadCompleted?.Invoke(this, completedDownload);
        }
        catch (Exception ex)
        {
            var failedDownload = download with { Status = DownloadStatus.Failed };
            _downloads.TryUpdate(downloadId, failedDownload, download);
        }
    }

    private static async Task<string> ComputeSha256HashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task ApplyMarkOfTheWebAsync(string filePath, string sourceUrl)
    {
        try
        {
            var attachmentExecute = (IAttachmentExecute)new AttachmentServices();
            
            attachmentExecute.SetLocalPath(filePath);
            attachmentExecute.SetFileName(Path.GetFileName(filePath));
            attachmentExecute.SetSource(sourceUrl);
            attachmentExecute.SetClientTitle("EphemeralBrowser");
            
            // This adds the Zone.Identifier alternate data stream
            attachmentExecute.Save();
        }
        catch (COMException)
        {
            // MOTW failed - continue without it (non-critical)
            // Alternative: Write Zone.Identifier manually
            await WriteZoneIdentifierAsync(filePath, sourceUrl);
        }
    }

    private static async Task WriteZoneIdentifierAsync(string filePath, string sourceUrl)
    {
        try
        {
            var zoneIdentifier = $"{filePath}:Zone.Identifier";
            var content = $"[ZoneTransfer]\r\nZoneId=3\r\nReferrerUrl={sourceUrl}\r\n";
            await File.WriteAllTextAsync(zoneIdentifier, content);
        }
        catch
        {
            // Best effort
        }
    }

    private static async Task SecureDeleteFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            var size = fileInfo.Length;

            // For small files, overwrite with random data before deletion
            if (size <= 1024 * 1024) // 1MB
            {
                var randomData = new byte[size];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomData);
                await File.WriteAllBytesAsync(filePath, randomData);
            }

            File.Delete(filePath);
            
            // Also try to delete Zone.Identifier stream
            try { File.Delete($"{filePath}:Zone.Identifier"); } catch { }
        }
        catch
        {
            // Best effort
        }
    }

    private bool IsContentTypeAllowed(string contentType)
    {
        return _allowedContentTypes.Any(allowed => 
            contentType.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsExtensionBlocked(string extension)
    {
        return _blockedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void OnDOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        // Inject download security script
        _ = Task.Run(async () =>
        {
            try
            {
                var script = GetDownloadSecurityScript();
                await _webView!.AddScriptToExecuteOnDocumentCreatedAsync(script);
            }
            catch
            {
                // Non-critical
            }
        });
    }

    private static string GetDownloadSecurityScript()
    {
        return """
            (function() {
                'use strict';
                
                const originalCreateElement = document.createElement;
                document.createElement = function(tagName) {
                    const element = originalCreateElement.call(this, tagName);
                    
                    if (tagName.toLowerCase() === 'a' && element instanceof HTMLAnchorElement) {
                        const originalSetAttribute = element.setAttribute;
                        element.setAttribute = function(name, value) {
                            if (name.toLowerCase() === 'download' && typeof value === 'string') {
                                const ext = value.toLowerCase().split('.').pop();
                                const blockedExts = ['exe', 'msi', 'bat', 'cmd', 'scr', 'pif', 'vbs', 'js'];
                                if (blockedExts.includes(ext)) {
                                    console.warn('EphemeralBrowser: Blocked potentially unsafe download:', value);
                                    return;
                                }
                            }
                            return originalSetAttribute.call(this, name, value);
                        };
                    }
                    
                    return element;
                };
            })();
            """;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_webView != null)
        {
            _webView.DownloadStarting -= OnDownloadStarting;
            _webView.DOMContentLoaded -= OnDOMContentLoaded;
        }

        // Clean up quarantined files
        _ = Task.Run(async () =>
        {
            var quarantinedDownloads = _downloads.Values
                .Where(d => d.Status == DownloadStatus.Quarantined)
                .ToArray();

            foreach (var download in quarantinedDownloads)
            {
                await DeleteDownloadAsync(download.DownloadId);
            }
        });
    }
}