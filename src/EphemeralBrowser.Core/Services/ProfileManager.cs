using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace EphemeralBrowser.Core.Services;

public sealed class ProfileManager : IProfileManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ProfileEntry> _profiles = new();
    private readonly string _baseUserDataPath;
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed;

    private readonly record struct ProfileEntry(
        string ProfileId,
        string UserDataFolder,
        CoreWebView2Environment? Environment,
        DateTime CreatedAt,
        TaskCompletionSource<bool> DisposalCompletion);

    public ProfileManager(string? baseUserDataPath = null)
    {
        _baseUserDataPath = baseUserDataPath ?? Path.Combine(Path.GetTempPath(), "EphemeralBrowser");
        Directory.CreateDirectory(_baseUserDataPath);
        
        _cleanupTimer = new Timer(async _ => await CleanupOrphanedProfilesAsync(), 
            null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<ProfileInfo> CreateEphemeralProfileAsync(string? sessionId = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProfileManager));

        var profileId = sessionId ?? GenerateProfileId();
        var userDataFolder = Path.Combine(_baseUserDataPath, profileId);
        
        try
        {
            Directory.CreateDirectory(userDataFolder);
            
            var environmentOptions = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = GetSecurityArguments()
            };
            
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: environmentOptions);

            var disposalCompletion = new TaskCompletionSource<bool>();
            var entry = new ProfileEntry(profileId, userDataFolder, environment, DateTime.UtcNow, disposalCompletion);
            
            if (!_profiles.TryAdd(profileId, entry))
            {
                // Environment cleanup - no direct Dispose method
                await Task.Delay(50); // Brief delay for cleanup
                Directory.Delete(userDataFolder, recursive: true);
                throw new InvalidOperationException($"Profile {profileId} already exists");
            }

            return new ProfileInfo(profileId, userDataFolder, DateTime.UtcNow, true);
        }
        catch
        {
            if (Directory.Exists(userDataFolder))
            {
                try { Directory.Delete(userDataFolder, recursive: true); } catch { }
            }
            throw;
        }
    }

    public async Task DisposeProfileAsync(string profileId)
    {
        if (!_profiles.TryRemove(profileId, out var entry))
            return;

        try
        {
            // Phase 1: Close WebView2 environment
            if (entry.Environment != null)
            {
                // WebView2 environments don't have a direct Dispose method
                // They are disposed automatically when all WebView2 controls are disposed
                // We just need to wait a bit for cleanup
                await Task.Delay(100);
            }

            // Phase 2: Secure directory wipe with retry logic
            await SecureDirectoryWipeAsync(entry.UserDataFolder);
            
            entry.DisposalCompletion.SetResult(true);
        }
        catch (Exception ex)
        {
            entry.DisposalCompletion.SetException(ex);
            throw;
        }
    }

    public async Task<CoreWebView2Environment?> GetEnvironmentAsync(string profileId)
    {
        await Task.CompletedTask; // Make method truly async
        return _profiles.TryGetValue(profileId, out var entry) ? entry.Environment : null;
    }

    public ProfileInfo[] GetActiveProfiles()
    {
        return _profiles.Values
            .Select(e => new ProfileInfo(e.ProfileId, e.UserDataFolder, e.CreatedAt, true))
            .ToArray();
    }

    public async Task CleanupOrphanedProfilesAsync()
    {
        if (!await _cleanupSemaphore.WaitAsync(100))
            return;

        try
        {
            if (!Directory.Exists(_baseUserDataPath))
                return;

            var activeProfileDirs = _profiles.Values.Select(p => p.UserDataFolder).ToHashSet();
            var allProfileDirs = Directory.GetDirectories(_baseUserDataPath);

            var orphanedDirs = allProfileDirs.Where(dir => !activeProfileDirs.Contains(dir));
            
            foreach (var orphanedDir in orphanedDirs)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(orphanedDir);
                    if (IsProfileDirectoryStale(dirInfo))
                    {
                        await SecureDirectoryWipeAsync(orphanedDir);
                    }
                }
                catch
                {
                    // Continue cleanup even if some directories fail
                }
            }
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }

    private static string GenerateProfileId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var randomBytes = new byte[8];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        
        var combined = BitConverter.GetBytes(timestamp).Concat(randomBytes).ToArray();
        return Convert.ToHexString(combined).ToLowerInvariant();
    }

    private static string GetSecurityArguments()
    {
        return string.Join(" ", new[]
        {
            "--disable-web-security",
            "--disable-site-isolation-trials",
            "--disable-features=VizDisplayCompositor",
            "--process-per-site",
            "--disable-background-networking",
            "--disable-background-timer-throttling",
            "--disable-backgrounding-occluded-windows",
            "--disable-renderer-backgrounding",
            "--disable-field-trial-config",
            "--no-default-browser-check",
            "--no-first-run",
            "--disable-default-apps"
        });
    }

    private static async Task SecureDirectoryWipeAsync(string directoryPath)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 100;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return;

                // First pass: overwrite small files for security
                await OverwriteSmallFilesAsync(directoryPath);
                
                // Second pass: recursive delete
                Directory.Delete(directoryPath, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(retryDelayMs * (attempt + 1));
                await ClearReadOnlyAttributesAsync(directoryPath);
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(retryDelayMs * (attempt + 1));
            }
        }
    }

    private static async Task OverwriteSmallFilesAsync(string directoryPath)
    {
        const long maxOverwriteSize = 1024 * 1024; // 1MB

        try
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            var smallFiles = files.Where(f => new FileInfo(f).Length <= maxOverwriteSize);

            await Parallel.ForEachAsync(smallFiles, async (filePath, ct) =>
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.IsReadOnly)
                        fileInfo.IsReadOnly = false;

                    var randomData = new byte[fileInfo.Length];
                    using var rng = RandomNumberGenerator.Create();
                    rng.GetBytes(randomData);

                    await File.WriteAllBytesAsync(filePath, randomData, ct);
                }
                catch
                {
                    // Best effort - continue with other files
                }
            });
        }
        catch
        {
            // Best effort overwrite
        }
    }

    private static async Task ClearReadOnlyAttributesAsync(string directoryPath)
    {
        try
        {
            await Task.Run(() =>
            {
                var directoryInfo = new DirectoryInfo(directoryPath);
                if (!directoryInfo.Exists)
                    return;

                SetAttributesRecursive(directoryInfo, FileAttributes.Normal);
            });
        }
        catch
        {
            // Best effort
        }
    }

    private static void SetAttributesRecursive(DirectoryInfo dir, FileAttributes attributes)
    {
        try
        {
            foreach (var file in dir.GetFiles())
            {
                try { file.Attributes = attributes; } catch { }
            }

            foreach (var subDir in dir.GetDirectories())
            {
                try
                {
                    SetAttributesRecursive(subDir, attributes);
                    subDir.Attributes = attributes;
                }
                catch { }
            }
        }
        catch { }
    }

    private static bool IsProfileDirectoryStale(DirectoryInfo directory)
    {
        try
        {
            var ageThreshold = DateTime.UtcNow.AddHours(-1);
            return directory.CreationTimeUtc < ageThreshold && 
                   directory.LastWriteTimeUtc < ageThreshold;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        _cleanupTimer?.Dispose();
        
        var disposalTasks = _profiles.Values
            .Select(entry => DisposeProfileAsync(entry.ProfileId))
            .ToArray();

        try
        {
            await Task.WhenAll(disposalTasks);
        }
        catch
        {
            // Best effort cleanup
        }

        _cleanupSemaphore?.Dispose();
        
        try
        {
            if (Directory.Exists(_baseUserDataPath))
            {
                Directory.Delete(_baseUserDataPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}