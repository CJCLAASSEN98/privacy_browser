using Microsoft.Web.WebView2.Core;

namespace EphemeralBrowser.Core.Services;

public readonly record struct ProfileInfo(
    string ProfileId,
    string UserDataFolder,
    DateTime CreatedAt,
    bool IsActive);

public interface IProfileManager
{
    Task<ProfileInfo> CreateEphemeralProfileAsync(string? sessionId = null);
    Task DisposeProfileAsync(string profileId);
    Task<CoreWebView2Environment?> GetEnvironmentAsync(string profileId);
    ProfileInfo[] GetActiveProfiles();
    Task CleanupOrphanedProfilesAsync();
}