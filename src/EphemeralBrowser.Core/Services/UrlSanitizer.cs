/*
 * EphemeralBrowser - Privacy-First Ephemeral Browser
 * Copyright © 2025 EphemeralBrowser. All Rights Reserved.
 * 
 * Commercial use prohibited without license. Contact: claassen.cjs@gmail.com
 * For licensing terms and commercial use, see LICENSE file.
 */

/*
 * EphemeralBrowser - Privacy-First Ephemeral Browser
 * Copyright © 2025 EphemeralBrowser. All Rights Reserved.
 * 
 * Commercial use prohibited without license. Contact: claassen.cjs@gmail.com
 * For licensing terms and commercial use, see LICENSE file.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EphemeralBrowser.Core.Services;

public sealed class UrlSanitizer : IUrlSanitizer
{
    private readonly ConcurrentDictionary<string, SanitizationMetrics> _domainMetrics = new();
    private readonly ConcurrentDictionary<string, List<string>> _domainAllowedParams = new();
    private volatile Regex[] _trackingParamRegexes = Array.Empty<Regex>();
    private volatile string[] _trackingParams =
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "fbclid", "gclid", "mc_eid", "igshid", "_ga", "msclkid",
        "twclid", "li_fat_id", "s_cid", "vero_conv", "vero_id",
        "wickedid", "yclid", "_openstat", "pk_campaign", "pk_kwd"
    };

    public UrlSanitizer()
    {
        CompileRegexes();
    }

    public SanitizationResult Sanitize(string url)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new SanitizationResult(url, url, Array.Empty<string>(), stopwatch.Elapsed);

        var domain = uri.Host.ToLowerInvariant();
        var allowedParams = _domainAllowedParams.GetValueOrDefault(domain, new List<string>());
        
        var query = uri.Query;
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            UpdateMetrics(domain, false, stopwatch.Elapsed);
            return new SanitizationResult(url, url, Array.Empty<string>(), stopwatch.Elapsed);
        }

        var removedParams = new List<string>();
        var cleanParams = new List<string>();
        
        var paramPairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var param in paramPairs)
        {
            var equalIndex = param.IndexOf('=');
            var paramName = equalIndex > 0 ? param[..equalIndex] : param;
            
            if (ShouldRemoveParameter(paramName, allowedParams))
            {
                removedParams.Add(paramName);
            }
            else
            {
                cleanParams.Add(param);
            }
        }

        var sanitizedUrl = removedParams.Count > 0
            ? BuildCleanUrl(uri, cleanParams)
            : url;

        var wasSanitized = removedParams.Count > 0;
        UpdateMetrics(domain, wasSanitized, stopwatch.Elapsed);

        return new SanitizationResult(url, sanitizedUrl, removedParams.ToArray(), stopwatch.Elapsed);
    }

    public SanitizationMetrics GetDomainMetrics(string domain)
    {
        return _domainMetrics.GetValueOrDefault(domain.ToLowerInvariant(), 
            new SanitizationMetrics(domain, 0, 0, 0.0, DateTime.MinValue));
    }

    public void LoadRules(string rulesJson)
    {
        try
        {
            var rules = JsonSerializer.Deserialize<UrlSanitizerRules>(rulesJson);
            if (rules?.TrackingParams != null)
            {
                _trackingParams = rules.TrackingParams;
                CompileRegexes();
            }

            if (rules?.DomainAllowedParams != null)
            {
                _domainAllowedParams.Clear();
                foreach (var kvp in rules.DomainAllowedParams)
                {
                    _domainAllowedParams[kvp.Key.ToLowerInvariant()] = kvp.Value.ToList();
                }
            }
        }
        catch (JsonException)
        {
            // Fail-closed: keep existing rules on parse error
        }
    }

    private bool ShouldRemoveParameter(string paramName, List<string> allowedParams)
    {
        if (allowedParams.Contains(paramName, StringComparer.OrdinalIgnoreCase))
            return false;

        if (_trackingParams.Contains(paramName, StringComparer.OrdinalIgnoreCase))
            return true;

        return _trackingParamRegexes.Any(regex => regex.IsMatch(paramName));
    }

    private void CompileRegexes()
    {
        var patterns = new[]
        {
            @"^utm_.*",
            @"^_ga.*",
            @"^fb.*",
            @"^gc.*",
            @".*clid$",
            @"^pk_.*",
            @"^_.*tracking.*",
            @".*_campaign.*",
            @".*_source.*"
        };

        _trackingParamRegexes = patterns
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();
    }

    private static string BuildCleanUrl(Uri uri, List<string> cleanParams)
    {
        var uriBuilder = new UriBuilder(uri) { Query = string.Empty };
        
        if (cleanParams.Count > 0)
        {
            uriBuilder.Query = string.Join('&', cleanParams);
        }

        return uriBuilder.ToString();
    }

    private void UpdateMetrics(string domain, bool wasSanitized, TimeSpan processingTime)
    {
        _domainMetrics.AddOrUpdate(domain,
            new SanitizationMetrics(domain, 1, wasSanitized ? 1 : 0, processingTime.TotalMilliseconds, DateTime.UtcNow),
            (_, existing) =>
            {
                var newTotal = existing.TotalRequests + 1;
                var newSanitized = existing.SanitizedRequests + (wasSanitized ? 1 : 0);
                var newAverage = ((existing.AverageLatencyMs * existing.TotalRequests) + processingTime.TotalMilliseconds) / newTotal;
                
                return new SanitizationMetrics(domain, newTotal, newSanitized, newAverage, DateTime.UtcNow);
            });
    }
}

public sealed record UrlSanitizerRules(
    string[] TrackingParams,
    Dictionary<string, string[]> DomainAllowedParams);