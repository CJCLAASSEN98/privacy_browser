using System.Text.Json;
using Xunit;

namespace EphemeralBrowser.Privacy.Tests;

public sealed class UrlSanitizerTests
{
    private readonly UrlSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_RemovesCommonTrackingParams()
    {
        var url = "https://example.com/page?utm_source=google&utm_medium=cpc&normal_param=value";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://example.com/page?normal_param=value", result.SanitizedUrl);
        Assert.Contains("utm_source", result.RemovedParams);
        Assert.Contains("utm_medium", result.RemovedParams);
        Assert.Equal(2, result.RemovedParams.Length);
    }

    [Fact]
    public void Sanitize_HandlesFacebookTrackingParams()
    {
        var url = "https://example.com/article?fbclid=IwAR123&other=test";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://example.com/article?other=test", result.SanitizedUrl);
        Assert.Contains("fbclid", result.RemovedParams);
    }

    [Fact]
    public void Sanitize_HandlesGoogleAnalyticsParams()
    {
        var url = "https://shop.example.com/product?_ga=GA1.2.123&gclid=abc&product_id=456";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://shop.example.com/product?product_id=456", result.SanitizedUrl);
        Assert.Contains("_ga", result.RemovedParams);
        Assert.Contains("gclid", result.RemovedParams);
    }

    [Fact]
    public void Sanitize_PreservesUrlWithoutTrackingParams()
    {
        var url = "https://example.com/page?id=123&category=tech";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal(url, result.SanitizedUrl);
        Assert.Empty(result.RemovedParams);
    }

    [Fact]
    public void Sanitize_HandlesUrlWithoutQuery()
    {
        var url = "https://example.com/page";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal(url, result.SanitizedUrl);
        Assert.Empty(result.RemovedParams);
    }

    [Fact]
    public void Sanitize_HandlesInvalidUrl()
    {
        var invalidUrl = "not-a-url";
        
        var result = _sanitizer.Sanitize(invalidUrl);
        
        Assert.Equal(invalidUrl, result.SanitizedUrl);
        Assert.Empty(result.RemovedParams);
    }

    [Fact]
    public void Sanitize_RemovesAllTrackingParamsLeavingCleanUrl()
    {
        var url = "https://example.com/page?utm_source=twitter&utm_campaign=spring&fbclid=123";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://example.com/page", result.SanitizedUrl);
        Assert.Equal(3, result.RemovedParams.Length);
    }

    [Fact]
    public void LoadRules_AllowsDomainSpecificParams()
    {
        var rules = new UrlSanitizerRules(
            new[] { "utm_source", "utm_medium" },
            new Dictionary<string, string[]>
            {
                ["example.com"] = new[] { "utm_source" }
            });
        
        var rulesJson = JsonSerializer.Serialize(rules);
        _sanitizer.LoadRules(rulesJson);
        
        var url = "https://example.com/page?utm_source=allowed&utm_medium=blocked";
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://example.com/page?utm_source=allowed", result.SanitizedUrl);
        Assert.Contains("utm_medium", result.RemovedParams);
        Assert.DoesNotContain("utm_source", result.RemovedParams);
    }

    [Fact]
    public void GetDomainMetrics_TracksRequestCounts()
    {
        _sanitizer.Sanitize("https://example.com/page1?utm_source=test");
        _sanitizer.Sanitize("https://example.com/page2?normal=param");
        _sanitizer.Sanitize("https://example.com/page3?utm_medium=test");
        
        var metrics = _sanitizer.GetDomainMetrics("example.com");
        
        Assert.Equal("example.com", metrics.Domain);
        Assert.Equal(3, metrics.TotalRequests);
        Assert.Equal(2, metrics.SanitizedRequests);
        Assert.True(metrics.AverageLatencyMs >= 0);
    }

    [Fact]
    public void Sanitize_MeasuresProcessingTime()
    {
        var url = "https://example.com/page?utm_source=test&other=param";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.True(result.ProcessingTime.TotalMilliseconds >= 0);
        Assert.True(result.ProcessingTime.TotalMilliseconds < 1000); // Should be fast
    }

    [Theory]
    [InlineData("utm_source=test")]
    [InlineData("utm_medium=email")]
    [InlineData("utm_campaign=spring2024")]
    [InlineData("fbclid=IwAR123")]
    [InlineData("gclid=abc123")]
    [InlineData("mc_eid=newsletter")]
    [InlineData("igshid=social")]
    [InlineData("_ga=GA1.2.123")]
    [InlineData("msclkid=microsoft")]
    public void Sanitize_RemovesKnownTrackingParam(string trackingParam)
    {
        var url = $"https://example.com/page?{trackingParam}&keep=this";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://example.com/page?keep=this", result.SanitizedUrl);
        Assert.Single(result.RemovedParams);
    }

    [Fact]
    public void LoadRules_HandlesInvalidJson()
    {
        var invalidJson = "{ invalid json }";
        
        // Should not throw and should keep existing rules
        _sanitizer.LoadRules(invalidJson);
        
        var url = "https://example.com/page?utm_source=test";
        var result = _sanitizer.Sanitize(url);
        
        // Should still remove known tracking params
        Assert.Equal("https://example.com/page", result.SanitizedUrl);
    }

    [Fact]
    public void Sanitize_HandlesComplexQueryString()
    {
        var url = "https://shop.example.com/search?q=laptops&category=electronics&utm_source=google&sort=price&utm_medium=cpc&page=1&fbclid=123";
        
        var result = _sanitizer.Sanitize(url);
        
        Assert.Equal("https://shop.example.com/search?q=laptops&category=electronics&sort=price&page=1", result.SanitizedUrl);
        Assert.Equal(3, result.RemovedParams.Length);
        Assert.Contains("utm_source", result.RemovedParams);
        Assert.Contains("utm_medium", result.RemovedParams);
        Assert.Contains("fbclid", result.RemovedParams);
    }
}