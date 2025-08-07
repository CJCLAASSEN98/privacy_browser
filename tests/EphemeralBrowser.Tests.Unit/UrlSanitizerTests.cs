using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using EphemeralBrowser.Core.Services;

namespace EphemeralBrowser.Tests.Unit;

public class UrlSanitizerTests
{
    private readonly UrlSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_RemovesCommonTrackingParams()
    {
        var url = "https://example.com/page?utm_source=google&utm_medium=cpc&normal_param=value";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be("https://example.com/page?normal_param=value");
        result.RemovedParams.Should().Contain("utm_source");
        result.RemovedParams.Should().Contain("utm_medium");
        result.RemovedParams.Should().HaveCount(2);
        result.ProcessingTime.Should().BeLessThan(TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public void Sanitize_HandlesFacebookTrackingParams()
    {
        var url = "https://example.com/article?fbclid=IwAR123&other=test";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be("https://example.com/article?other=test");
        result.RemovedParams.Should().Contain("fbclid");
        result.RemovedParams.Should().HaveCount(1);
    }

    [Fact]
    public void Sanitize_HandlesGoogleAnalyticsParams()
    {
        var url = "https://shop.example.com/product?_ga=GA1.2.123&gclid=abc&product_id=456";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be("https://shop.example.com/product?product_id=456");
        result.RemovedParams.Should().Contain("_ga");
        result.RemovedParams.Should().Contain("gclid");
        result.RemovedParams.Should().HaveCount(2);
    }

    [Fact]
    public void Sanitize_PreservesUrlWithoutTrackingParams()
    {
        var url = "https://example.com/page?id=123&category=tech";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be(url);
        result.RemovedParams.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_HandlesUrlWithoutQuery()
    {
        var url = "https://example.com/page";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be(url);
        result.RemovedParams.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_HandlesInvalidUrl()
    {
        var invalidUrl = "not-a-url";
        
        var result = _sanitizer.Sanitize(invalidUrl);
        
        result.SanitizedUrl.Should().Be(invalidUrl);
        result.RemovedParams.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_RemovesAllTrackingParamsLeavingCleanUrl()
    {
        var url = "https://example.com/page?utm_source=twitter&utm_campaign=spring&fbclid=123";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be("https://example.com/page");
        result.RemovedParams.Should().HaveCount(3);
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
        
        result.SanitizedUrl.Should().Be("https://example.com/page?utm_source=allowed");
        result.RemovedParams.Should().Contain("utm_medium");
        result.RemovedParams.Should().NotContain("utm_source");
    }

    [Fact]
    public void GetDomainMetrics_TracksRequestCounts()
    {
        _sanitizer.Sanitize("https://example.com/page1?utm_source=test");
        _sanitizer.Sanitize("https://example.com/page2?normal=param");
        _sanitizer.Sanitize("https://example.com/page3?utm_medium=test");
        
        var metrics = _sanitizer.GetDomainMetrics("example.com");
        
        metrics.Domain.Should().Be("example.com");
        metrics.TotalRequests.Should().Be(3);
        metrics.SanitizedRequests.Should().Be(2);
        metrics.AverageLatencyMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Sanitize_MeetsPerformanceRequirements()
    {
        var url = "https://example.com/page?utm_source=test&utm_medium=email&utm_campaign=spring&fbclid=123&other=param";
        
        var result = _sanitizer.Sanitize(url);
        
        // Should meet <150ms sanitizer overhead requirement from PRP
        result.ProcessingTime.Should().BeLessThan(TimeSpan.FromMilliseconds(150));
        result.RemovedParams.Should().HaveCount(4);
        result.SanitizedUrl.Should().Be("https://example.com/page?other=param");
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
        
        result.SanitizedUrl.Should().Be("https://example.com/page?keep=this");
        result.RemovedParams.Should().HaveCount(1);
    }

    [Fact]
    public void LoadRules_HandlesInvalidJson()
    {
        var invalidJson = "{ invalid json }";
        
        // Should not throw and should keep existing rules
        var act = () => _sanitizer.LoadRules(invalidJson);
        act.Should().NotThrow();
        
        var url = "https://example.com/page?utm_source=test";
        var result = _sanitizer.Sanitize(url);
        
        // Should still remove known tracking params
        result.SanitizedUrl.Should().Be("https://example.com/page");
    }

    [Fact]
    public void Sanitize_HandlesComplexQueryString()
    {
        var url = "https://shop.example.com/search?q=laptops&category=electronics&utm_source=google&sort=price&utm_medium=cpc&page=1&fbclid=123";
        
        var result = _sanitizer.Sanitize(url);
        
        result.SanitizedUrl.Should().Be("https://shop.example.com/search?q=laptops&category=electronics&sort=price&page=1");
        result.RemovedParams.Should().HaveCount(3);
        result.RemovedParams.Should().Contain("utm_source");
        result.RemovedParams.Should().Contain("utm_medium");
        result.RemovedParams.Should().Contain("fbclid");
    }

    [Fact]
    public void Sanitize_HandlesEdgeCases()
    {
        // Test various edge cases
        var testCases = new[]
        {
            ("https://example.com?", "https://example.com?"),
            ("https://example.com?utm_source", "https://example.com?"),
            ("https://example.com?utm_source=", "https://example.com?"),
            ("https://example.com?utm_source=&other=value", "https://example.com?other=value"),
            ("https://example.com?other=value&utm_source=", "https://example.com?other=value")
        };

        foreach (var (input, expected) in testCases)
        {
            var result = _sanitizer.Sanitize(input);
            result.SanitizedUrl.Should().Be(expected, $"Failed for input: {input}");
        }
    }
}