/*
 * EphemeralBrowser - Privacy-First Ephemeral Browser
 * Copyright © 2025 EphemeralBrowser. All Rights Reserved.
 * 
 * Commercial use prohibited without license. Contact: claassen.cjs@gmail.com
 * For licensing terms and commercial use, see LICENSE file.
 */

using System;
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