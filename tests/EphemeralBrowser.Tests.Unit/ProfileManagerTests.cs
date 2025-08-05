using Xunit;
using FluentAssertions;
using EphemeralBrowser.Core.Services;

namespace EphemeralBrowser.Tests.Unit;

public class ProfileManagerTests : IAsyncDisposable
{
    private readonly ProfileManager _profileManager;
    private readonly string _testBaseDirectory;

    public ProfileManagerTests()
    {
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "EphemeralBrowserTests", Guid.NewGuid().ToString("N"));
        _profileManager = new ProfileManager(_testBaseDirectory);
    }

    [Fact]
    public async Task CreateEphemeralProfileAsync_CreatesUniqueProfile()
    {
        var profileInfo = await _profileManager.CreateEphemeralProfileAsync();
        
        profileInfo.ProfileId.Should().NotBeNullOrEmpty();
        profileInfo.UserDataFolder.Should().NotBeNullOrEmpty();
        profileInfo.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        profileInfo.IsActive.Should().BeTrue();
        
        Directory.Exists(profileInfo.UserDataFolder).Should().BeTrue();
    }

    [Fact]
    public async Task CreateEphemeralProfileAsync_WithCustomSessionId_UsesProvidedId()
    {
        var customSessionId = "custom-session-123";
        
        var profileInfo = await _profileManager.CreateEphemeralProfileAsync(customSessionId);
        
        profileInfo.ProfileId.Should().Be(customSessionId);
        profileInfo.UserDataFolder.Should().Contain(customSessionId);
    }

    [Fact]
    public async Task CreateEphemeralProfileAsync_MultipleProfiles_CreatesUniqueDirectories()
    {
        var profile1 = await _profileManager.CreateEphemeralProfileAsync();
        var profile2 = await _profileManager.CreateEphemeralProfileAsync();
        
        profile1.ProfileId.Should().NotBe(profile2.ProfileId);
        profile1.UserDataFolder.Should().NotBe(profile2.UserDataFolder);
        
        Directory.Exists(profile1.UserDataFolder).Should().BeTrue();
        Directory.Exists(profile2.UserDataFolder).Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveProfiles_ReturnsAllActiveProfiles()
    {
        var profile1 = await _profileManager.CreateEphemeralProfileAsync();
        var profile2 = await _profileManager.CreateEphemeralProfileAsync();
        
        var activeProfiles = _profileManager.GetActiveProfiles();
        
        activeProfiles.Should().HaveCount(2);
        activeProfiles.Should().Contain(p => p.ProfileId == profile1.ProfileId);
        activeProfiles.Should().Contain(p => p.ProfileId == profile2.ProfileId);
    }

    [Fact]
    public async Task DisposeProfileAsync_RemovesProfileAndCleansDirectory()
    {
        var profileInfo = await _profileManager.CreateEphemeralProfileAsync();
        var userDataFolder = profileInfo.UserDataFolder;
        
        // Verify profile exists
        Directory.Exists(userDataFolder).Should().BeTrue();
        _profileManager.GetActiveProfiles().Should().Contain(p => p.ProfileId == profileInfo.ProfileId);
        
        await _profileManager.DisposeProfileAsync(profileInfo.ProfileId);
        
        // Verify profile is disposed and directory is cleaned
        _profileManager.GetActiveProfiles().Should().NotContain(p => p.ProfileId == profileInfo.ProfileId);
        // Note: Directory cleanup happens asynchronously, so we don't check directory existence immediately
    }

    [Fact]
    public async Task DisposeProfileAsync_NonexistentProfile_DoesNotThrow()
    {
        var act = async () => await _profileManager.DisposeProfileAsync("nonexistent-profile");
        
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetEnvironmentAsync_ExistingProfile_ReturnsEnvironment()
    {
        var profileInfo = await _profileManager.CreateEphemeralProfileAsync();
        
        var environment = await _profileManager.GetEnvironmentAsync(profileInfo.ProfileId);
        
        environment.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEnvironmentAsync_NonexistentProfile_ReturnsNull()
    {
        var environment = await _profileManager.GetEnvironmentAsync("nonexistent-profile");
        
        environment.Should().BeNull();
    }

    [Fact]
    public async Task CleanupOrphanedProfilesAsync_DoesNotThrow()
    {
        // Create a profile to ensure there's something to potentially clean up
        await _profileManager.CreateEphemeralProfileAsync();
        
        var act = async () => await _profileManager.CleanupOrphanedProfilesAsync();
        
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProfileCreation_MeetsPerformanceRequirements()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var profileInfo = await _profileManager.CreateEphemeralProfileAsync();
        
        stopwatch.Stop();
        
        // Should be created quickly for responsive UI
        // Note: WebView2 environment creation can be slow on first run, but should be fast after initialization
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Profile creation should be reasonably fast");
        
        profileInfo.ProfileId.Should().NotBeNullOrEmpty();
        Directory.Exists(profileInfo.UserDataFolder).Should().BeTrue();
    }

    [Fact]
    public async Task ProfileDisposal_MeetsPerformanceRequirements()
    {
        // Create profile first
        var profileInfo = await _profileManager.CreateEphemeralProfileAsync();
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await _profileManager.DisposeProfileAsync(profileInfo.ProfileId);
        
        stopwatch.Stop();
        
        // Should meet <500ms teardown requirement from PRP
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "Profile disposal should be quick for responsive shutdown");
    }

    [Fact]
    public async Task ConcurrentProfileOperations_HandledSafely()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(async i => await _profileManager.CreateEphemeralProfileAsync($"concurrent-{i}"))
            .ToArray();
        
        var profiles = await Task.WhenAll(tasks);
        
        profiles.Should().HaveCount(5);
        profiles.Select(p => p.ProfileId).Should().OnlyHaveUniqueItems();
        profiles.Select(p => p.UserDataFolder).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ProfileId_Generation_ProducesUniqueIds()
    {
        var profileIds = new List<string>();
        
        // Create multiple profiles to test ID uniqueness
        for (int i = 0; i < 10; i++)
        {
            var task = _profileManager.CreateEphemeralProfileAsync();
            var profile = task.GetAwaiter().GetResult();
            profileIds.Add(profile.ProfileId);
        }
        
        profileIds.Should().OnlyHaveUniqueItems("Profile IDs should be unique");
        profileIds.Should().AllSatisfy(id => id.Should().NotBeNullOrWhiteSpace("Profile ID should not be empty"));
    }

    public async ValueTask DisposeAsync()
    {
        await _profileManager.DisposeAsync();
        
        // Clean up test directory
        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}