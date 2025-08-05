using System.Collections.Concurrent;

namespace EphemeralBrowser.Core.Services;

public readonly record struct SanitizationResult(
    string OriginalUrl,
    string SanitizedUrl,
    string[] RemovedParams,
    TimeSpan ProcessingTime);

public readonly record struct SanitizationMetrics(
    string Domain,
    int TotalRequests,
    int SanitizedRequests,
    double AverageLatencyMs,
    DateTime LastUpdate);

public interface IUrlSanitizer
{
    SanitizationResult Sanitize(string url);
    SanitizationMetrics GetDomainMetrics(string domain);
    void LoadRules(string rulesJson);
}