using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EphemeralBrowser.Core.Services;
using System.Text.Json;

namespace EphemeralBrowser.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<UrlSanitizerBenchmarks>();
        Console.WriteLine(summary);
    }
}

[MemoryDiagnoser]
[SimpleJob]
public class UrlSanitizerBenchmarks
{
    private UrlSanitizer _sanitizer = null!;
    private readonly string[] _testUrls = 
    {
        "https://example.com/page?utm_source=test&utm_medium=email&utm_campaign=spring&normal=param",
        "https://shop.example.com/product?id=123&utm_source=google&fbclid=abc123&category=tech",
        "https://news.example.com/article?utm_source=twitter&utm_medium=social&utm_campaign=viral&_ga=GA1.2.123",
        "https://blog.example.com/post?gclid=click123&mc_eid=newsletter&igshid=social456&title=test",
        "https://site.example.com/path?normal1=value1&normal2=value2&normal3=value3", // No tracking params
        "https://complex.example.com/page?param1=value1&utm_source=complex&param2=value2&utm_medium=test&param3=value3&fbclid=tracking&param4=value4"
    };

    [GlobalSetup]
    public void Setup()
    {
        _sanitizer = new UrlSanitizer();
    }

    [Benchmark]
    public void SanitizeUrl_SingleTrackedUrl()
    {
        _sanitizer.Sanitize("https://example.com/page?utm_source=test&utm_medium=email&normal=param");
    }

    [Benchmark]
    public void SanitizeUrl_SingleCleanUrl()
    {
        _sanitizer.Sanitize("https://example.com/page?normal1=param1&normal2=param2");
    }

    [Benchmark]
    public void SanitizeUrl_ComplexTrackedUrl()
    {
        _sanitizer.Sanitize("https://example.com/page?param1=value1&utm_source=complex&param2=value2&utm_medium=test&param3=value3&fbclid=tracking&param4=value4&gclid=google123&_ga=analytics");
    }

    [Benchmark]
    public void SanitizeUrl_BatchProcessing()
    {
        foreach (var url in _testUrls)
        {
            _sanitizer.Sanitize(url);
        }
    }

    [Benchmark]
    public void LoadRules_JsonDeserialization()
    {
        var rules = new UrlSanitizerRules(
            new[] { "utm_source", "utm_medium", "utm_campaign", "fbclid", "gclid" },
            new Dictionary<string, string[]>
            {
                ["example.com"] = new[] { "utm_source" },
                ["test.com"] = new[] { "utm_medium", "fbclid" }
            });

        var json = JsonSerializer.Serialize(rules);
        _sanitizer.LoadRules(json);
    }

    [Benchmark]
    public void GetDomainMetrics_Performance()
    {
        // First generate some metrics
        for (int i = 0; i < 10; i++)
        {
            _sanitizer.Sanitize($"https://benchmark.com/page{i}?utm_source=test&normal=param{i}");
        }

        // Now benchmark metrics retrieval
        _sanitizer.GetDomainMetrics("benchmark.com");
    }

    [Benchmark]
    public void SanitizeUrl_ConcurrentAccess()
    {
        Parallel.ForEach(_testUrls, url =>
        {
            _sanitizer.Sanitize(url);
        });
    }
}

[MemoryDiagnoser]
[SimpleJob]
public class ProfileManagerBenchmarks
{
    private ProfileManager _profileManager = null!;
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "BenchmarkProfiles");

    [GlobalSetup]
    public void Setup()
    {
        _profileManager = new ProfileManager(_testDirectory);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _profileManager.DisposeAsync();
        
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Benchmark]
    public async Task CreateProfile_Performance()
    {
        var profile = await _profileManager.CreateEphemeralProfileAsync();
        await _profileManager.DisposeProfileAsync(profile.ProfileId);
    }

    [Benchmark]
    public async Task ProfileLifecycle_FullCycle()
    {
        // Create
        var profile = await _profileManager.CreateEphemeralProfileAsync();
        
        // Get environment
        var environment = await _profileManager.GetEnvironmentAsync(profile.ProfileId);
        
        // Get active profiles
        var activeProfiles = _profileManager.GetActiveProfiles();
        
        // Dispose
        await _profileManager.DisposeProfileAsync(profile.ProfileId);
    }

    [Benchmark]
    public async Task MultipleProfiles_Creation()
    {
        var profiles = new List<ProfileInfo>();
        
        // Create 5 profiles
        for (int i = 0; i < 5; i++)
        {
            var profile = await _profileManager.CreateEphemeralProfileAsync($"benchmark-{i}");
            profiles.Add(profile);
        }
        
        // Clean up
        foreach (var profile in profiles)
        {
            await _profileManager.DisposeProfileAsync(profile.ProfileId);
        }
    }
}

[MemoryDiagnoser]
[SimpleJob]
public class IntegrationBenchmarks
{
    private UrlSanitizer _urlSanitizer = null!;
    private ProfileManager _profileManager = null!;
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "IntegrationBenchmarks");

    [GlobalSetup]
    public void Setup()
    {
        _urlSanitizer = new UrlSanitizer();
        _profileManager = new ProfileManager(_testDirectory);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _profileManager.DisposeAsync();
        
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch { }
        }
    }

    [Benchmark]
    public async Task SimulateNavigation_WithSanitization()
    {
        // Create profile
        var profile = await _profileManager.CreateEphemeralProfileAsync();
        
        // Sanitize URL (simulating navigation)
        var result = _urlSanitizer.Sanitize("https://example.com/page?utm_source=benchmark&utm_medium=test&normal=param");
        
        // Get metrics
        var metrics = _urlSanitizer.GetDomainMetrics("example.com");
        
        // Cleanup
        await _profileManager.DisposeProfileAsync(profile.ProfileId);
    }

    [Benchmark]
    public async Task SimulateUserSession_MultipleNavigations()
    {
        // Create profile for session
        var profile = await _profileManager.CreateEphemeralProfileAsync();
        
        var urls = new[]
        {
            "https://google.com/search?q=test&utm_source=direct",
            "https://example.com/page?utm_medium=referral&fbclid=123",
            "https://shop.example.com/product?id=456&gclid=abc&utm_campaign=spring",
            "https://news.example.com/article?utm_source=social&_ga=analytics"
        };
        
        // Simulate multiple navigations
        foreach (var url in urls)
        {
            _urlSanitizer.Sanitize(url);
        }
        
        // Cleanup session
        await _profileManager.DisposeProfileAsync(profile.ProfileId);
    }
}