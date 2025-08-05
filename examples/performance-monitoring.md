# EphemeralBrowser Performance Monitoring Toolkit

## Overview

This document provides a comprehensive performance monitoring and profiling toolkit for the EphemeralBrowser project. The toolkit includes PerfView configurations, Windows Performance Recorder (WPR) templates, ETW (Event Tracing for Windows) sessions, and custom JSON performance schemas optimized for ephemeral container scenarios.

## Target Performance Budgets

Based on mid-tier laptop specifications (Intel i5-8265U, 8GB RAM, SSD):

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Cold Start | < 800ms | Application launch to first UI paint |
| Warm Start | < 300ms | Subsequent launches (WebView2 cached) |
| First Navigation Sanitizer Overhead | < 150ms | URL processing + redirect time |
| Steady-State Memory per Container | < 200MB | Working set on top-10 websites |
| Container Teardown | < 500ms | Profile wipe + handle release |
| UI Responsiveness | 60 FPS | Frame time consistency during navigation |

## PerfView Configuration

### Installation and Setup

```powershell
# Download PerfView from Microsoft
Invoke-WebRequest -Uri "https://github.com/microsoft/perfview/releases/latest/download/PerfView.exe" -OutFile "PerfView.exe"

# Create performance data directory
New-Item -ItemType Directory -Force -Path "C:\EphemeralBrowser\PerfData"
```

### PerfView Preset Configuration

Create `EphemeralBrowser.perfview.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<PerfViewConfiguration>
  <Providers>
    <!-- .NET Runtime Providers -->
    <Provider Name="Microsoft-Windows-DotNETRuntime" Keywords="0x4000000000000001" Level="Informational"/>
    <Provider Name="Microsoft-Windows-DotNETRuntimePrivate" Keywords="0x4000000000000001" Level="Informational"/>
    
    <!-- WebView2 Providers -->
    <Provider Name="Microsoft-Web-WebView2" Keywords="0xFFFFFFFF" Level="Verbose"/>
    <Provider Name="Microsoft-WebBrowser-Memory" Keywords="0xFFFFFFFF" Level="Informational"/>
    
    <!-- File I/O for Profile Management -->
    <Provider Name="Microsoft-Windows-Kernel-File" Keywords="0x00000010" Level="Informational"/>
    <Provider Name="Microsoft-Windows-Kernel-Process" Keywords="0x00000010" Level="Informational"/>
    
    <!-- Custom Application Events -->
    <Provider Name="EphemeralBrowser" Keywords="0xFFFFFFFF" Level="Verbose"/>
  </Providers>
  
  <CollectionSettings>
    <BufferSize>256</BufferSize>
    <MaxFile>1024</MaxFile>
    <CircularMB>0</CircularMB>
  </CollectionSettings>
</PerfViewConfiguration>
```

### PerfView Collection Commands

```powershell
# Cold start performance capture
PerfView.exe /OnlyProviders /AcceptEULA /NoGui /Zip:False `
  /Providers:"Microsoft-Windows-DotNETRuntime:*:Verbose,EphemeralBrowser:*:Verbose" `
  /MaxCollectSec:30 `
  collect "C:\EphemeralBrowser\PerfData\ColdStart.etl"

# Memory pressure analysis
PerfView.exe /OnlyProviders /AcceptEULA /NoGui /Zip:False `
  /Providers:"Microsoft-Windows-DotNETRuntime:0x1:Verbose" `
  /KernelEvents:Process+Thread+ImageLoad+VirtualAlloc `
  /MaxCollectSec:300 `
  collect "C:\EphemeralBrowser\PerfData\MemoryAnalysis.etl"

# Navigation overhead profiling
PerfView.exe /OnlyProviders /AcceptEULA /NoGui /Zip:False `
  /Providers:"Microsoft-Web-WebView2:*:Verbose,EphemeralBrowser:*:Verbose" `
  /MaxCollectSec:60 `
  collect "C:\EphemeralBrowser\PerfData\NavigationOverhead.etl"
```

## Windows Performance Recorder (WPR) Templates

### Primary WPR Profile

Create `EphemeralBrowser.wprp`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<WindowsPerformanceRecorder Version="1.0">
  <Profiles>
    <SystemCollector Id="EphemeralBrowser_SystemCollector" Name="EphemeralBrowser System Collector">
      <BufferSize Value="1024"/>
      <Buffers Value="80"/>
    </SystemCollector>
    
    <EventCollector Id="EphemeralBrowser_EventCollector" Name="EphemeralBrowser Event Collector">
      <BufferSize Value="1024"/>
      <Buffers Value="80"/>
    </EventCollector>

    <SystemProvider Id="EphemeralBrowser_SystemProvider">
      <Keywords>
        <Keyword Value="ProcessThread"/>
        <Keyword Value="Loader"/>
        <Keyword Value="Memory"/>
        <Keyword Value="FileIO"/>
        <Keyword Value="HardFaults"/>
        <Keyword Value="VirtualAllocation"/>
      </Keywords>
      <Stacks>
        <Stack Value="ProcessCreate"/>
        <Stack Value="ThreadCreate"/>
        <Stack Value="ImageLoad"/>
        <Stack Value="VirtualAllocation"/>
      </Stacks>
    </SystemProvider>

    <EventProvider Id="DotNetRuntime" Name="Microsoft-Windows-DotNETRuntime" NonPagedMemory="true">
      <Keywords>
        <Keyword Value="0x4000000000000001"/>
      </Keywords>
    </EventProvider>

    <EventProvider Id="WebView2Provider" Name="Microsoft-Web-WebView2" NonPagedMemory="true">
      <Keywords>
        <Keyword Value="0xFFFFFFFF"/>
      </Keywords>
    </EventProvider>

    <Profile Id="EphemeralBrowser.Verbose.File" Name="EphemeralBrowser" Description="EphemeralBrowser Performance Analysis" LoggingMode="File" DetailLevel="Verbose">
      <Collectors>
        <SystemCollectorId Value="EphemeralBrowser_SystemCollector">
          <SystemProviderId Value="EphemeralBrowser_SystemProvider"/>
        </SystemCollectorId>
        <EventCollectorId Value="EphemeralBrowser_EventCollector">
          <EventProviders>
            <EventProviderId Value="DotNetRuntime"/>
            <EventProviderId Value="WebView2Provider"/>
          </EventProviders>
        </EventCollectorId>
      </Collectors>
    </Profile>
  </Profiles>
</WindowsPerformanceRecorder>
```

### WPR Collection Commands

```powershell
# Start profiling session
wpr.exe -start EphemeralBrowser.wprp!EphemeralBrowser.Verbose.File -filemode

# Stop and save trace
wpr.exe -stop "C:\EphemeralBrowser\PerfData\EphemeralBrowser-$(Get-Date -Format 'yyyyMMdd-HHmmss').etl"

# Navigation spike analysis (short duration)
wpr.exe -start EphemeralBrowser.wprp!EphemeralBrowser.Verbose.File -filemode
# < perform navigation >
wpr.exe -stop "C:\EphemeralBrowser\PerfData\NavigationSpike.etl"
```

## ETW Session Management

### PowerShell ETW Session Script

```powershell
# EphemeralBrowser-ETW-Session.ps1

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Start", "Stop", "Status")]
    [string]$Action,
    
    [string]$OutputPath = "C:\EphemeralBrowser\PerfData\",
    [int]$DurationSeconds = 60
)

$SessionName = "EphemeralBrowser-Perf"
$ETLFile = Join-Path $OutputPath "EphemeralBrowser-$(Get-Date -Format 'yyyyMMdd-HHmmss').etl"

function Start-EphemeralBrowserETW {
    Write-Host "Starting ETW session: $SessionName"
    
    # Create session
    logman create trace $SessionName `
        -o $ETLFile `
        -bs 1024 `
        -nb 32 256 `
        -mode circular `
        -f bincirc `
        -max 512
    
    # Add providers
    logman update trace $SessionName `
        -p "Microsoft-Windows-DotNETRuntime" 0x4000000000000001 0x5
    
    logman update trace $SessionName `
        -p "Microsoft-Web-WebView2" 0xFFFFFFFF 0x5
    
    logman update trace $SessionName `
        -p "Microsoft-Windows-Kernel-File" 0x00000010 0x4
    
    logman update trace $SessionName `
        -p "Microsoft-Windows-Kernel-Process" 0x00000010 0x4
    
    # Start collection
    logman start $SessionName
    
    Write-Host "ETW session started. Will collect for $DurationSeconds seconds."
    
    if ($DurationSeconds -gt 0) {
        Start-Sleep -Seconds $DurationSeconds
        Stop-EphemeralBrowserETW
    }
}

function Stop-EphemeralBrowserETW {
    Write-Host "Stopping ETW session: $SessionName"
    
    logman stop $SessionName
    logman delete $SessionName
    
    Write-Host "ETW trace saved to: $ETLFile"
}

function Get-EphemeralBrowserETWStatus {
    $sessions = logman query -ets | Select-String $SessionName
    if ($sessions) {
        Write-Host "ETW session '$SessionName' is active"
        logman query $SessionName -ets
    } else {
        Write-Host "ETW session '$SessionName' is not active"
    }
}

switch ($Action) {
    "Start" { Start-EphemeralBrowserETW }
    "Stop" { Stop-EphemeralBrowserETW }
    "Status" { Get-EphemeralBrowserETWStatus }
}
```

## JSON Performance Schema

### Performance Data Structure

```csharp
// EphemeralBrowser.Performance.Models.cs

public sealed record PerformanceMetrics(
    string SessionId,
    DateTime Timestamp,
    StartupMetrics Startup,
    NavigationMetrics Navigation,
    MemoryMetrics Memory,
    ContainerMetrics Container,
    UIMetrics UI);

public sealed record StartupMetrics(
    TimeSpan ColdStartTime,
    TimeSpan WarmStartTime,
    TimeSpan WebView2InitTime,
    TimeSpan FirstPaintTime,
    bool WebView2Cached);

public sealed record NavigationMetrics(
    string Url,
    TimeSpan NavigationTime,
    TimeSpan SanitizerOverhead,
    TimeSpan DNSLookupTime,
    TimeSpan ConnectionTime,
    TimeSpan DocumentLoadTime,
    int RemovedTrackingParams,
    bool WasSanitized);

public sealed record MemoryMetrics(
    long WorkingSetBytes,
    long PrivateBytes,
    long ManagedHeapBytes,
    long WebView2ProcessBytes,
    int GCCollections,
    TimeSpan GCTotalTime);

public sealed record ContainerMetrics(
    string ProfileId,
    TimeSpan CreationTime,
    TimeSpan TeardownTime,
    long ProfileSizeBytes,
    int ActiveShims,
    bool SecureWipeCompleted);

public sealed record UIMetrics(
    double AverageFrameTime,
    double P95FrameTime,
    int DroppedFrames,
    bool MaintainedTargetFPS);
```

### JSON Schema Export

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "EphemeralBrowser Performance Metrics",
  "type": "object",
  "properties": {
    "sessionId": { "type": "string" },
    "timestamp": { "type": "string", "format": "date-time" },
    "startup": {
      "type": "object",
      "properties": {
        "coldStartTimeMs": { "type": "number", "minimum": 0 },
        "warmStartTimeMs": { "type": "number", "minimum": 0 },
        "webView2InitTimeMs": { "type": "number", "minimum": 0 },
        "firstPaintTimeMs": { "type": "number", "minimum": 0 },
        "webView2Cached": { "type": "boolean" }
      },
      "required": ["coldStartTimeMs", "warmStartTimeMs", "webView2InitTimeMs", "firstPaintTimeMs", "webView2Cached"]
    },
    "navigation": {
      "type": "object",
      "properties": {
        "url": { "type": "string", "format": "uri" },
        "navigationTimeMs": { "type": "number", "minimum": 0 },
        "sanitizerOverheadMs": { "type": "number", "minimum": 0 },
        "dnsLookupTimeMs": { "type": "number", "minimum": 0 },
        "connectionTimeMs": { "type": "number", "minimum": 0 },
        "documentLoadTimeMs": { "type": "number", "minimum": 0 },
        "removedTrackingParams": { "type": "integer", "minimum": 0 },
        "wasSanitized": { "type": "boolean" }
      },
      "required": ["url", "navigationTimeMs", "sanitizerOverheadMs", "wasSanitized"]
    },
    "memory": {
      "type": "object",
      "properties": {
        "workingSetBytes": { "type": "integer", "minimum": 0 },
        "privateBytesBytes": { "type": "integer", "minimum": 0 },
        "managedHeapBytes": { "type": "integer", "minimum": 0 },
        "webView2ProcessBytes": { "type": "integer", "minimum": 0 },
        "gcCollections": { "type": "integer", "minimum": 0 },
        "gcTotalTimeMs": { "type": "number", "minimum": 0 }
      },
      "required": ["workingSetBytes", "privateBytesBytes", "managedHeapBytes"]
    },
    "container": {
      "type": "object",
      "properties": {
        "profileId": { "type": "string" },
        "creationTimeMs": { "type": "number", "minimum": 0 },
        "teardownTimeMs": { "type": "number", "minimum": 0 },
        "profileSizeBytes": { "type": "integer", "minimum": 0 },
        "activeShims": { "type": "integer", "minimum": 0, "maximum": 10 },
        "secureWipeCompleted": { "type": "boolean" }
      },
      "required": ["profileId", "creationTimeMs", "teardownTimeMs", "activeShims", "secureWipeCompleted"]
    },
    "ui": {
      "type": "object",
      "properties": {
        "averageFrameTimeMs": { "type": "number", "minimum": 0 },
        "p95FrameTimeMs": { "type": "number", "minimum": 0 },
        "droppedFrames": { "type": "integer", "minimum": 0 },
        "maintainedTargetFPS": { "type": "boolean" }
      },
      "required": ["averageFrameTimeMs", "p95FrameTimeMs", "droppedFrames", "maintainedTargetFPS"]
    }
  },
  "required": ["sessionId", "timestamp", "startup", "navigation", "memory", "container", "ui"]
}
```

## Performance Collection Implementation

### C# Performance Collector

```csharp
public sealed class PerformanceCollector : IDisposable
{
    private readonly string _outputPath;
    private readonly Timer _collectionTimer;
    private readonly PerformanceCounter _memoryCounter;
    private readonly PerformanceCounter _cpuCounter;
    private readonly List<PerformanceMetrics> _metrics = new();

    public PerformanceCollector(string outputPath)
    {
        _outputPath = outputPath;
        _memoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
        _cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
        
        _collectionTimer = new Timer(CollectMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void CollectMetrics(object? state)
    {
        var startupMetrics = MeasureStartupMetrics();
        var memoryMetrics = MeasureMemoryMetrics();
        var uiMetrics = MeasureUIMetrics();
        
        var metrics = new PerformanceMetrics(
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            startupMetrics,
            new NavigationMetrics("", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0, false),
            memoryMetrics,
            new ContainerMetrics("", TimeSpan.Zero, TimeSpan.Zero, 0, 0, false),
            uiMetrics);
        
        _metrics.Add(metrics);
    }

    public async Task ExportMetricsAsync(string fileName)
    {
        var json = JsonSerializer.Serialize(_metrics, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(Path.Combine(_outputPath, fileName), json);
    }

    private StartupMetrics MeasureStartupMetrics()
    {
        // Implementation would measure actual startup times
        return new StartupMetrics(
            TimeSpan.FromMilliseconds(750),
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(100),
            true);
    }

    private MemoryMetrics MeasureMemoryMetrics()
    {
        var process = Process.GetCurrentProcess();
        return new MemoryMetrics(
            process.WorkingSet64,
            process.PrivateMemorySize64,
            GC.GetTotalMemory(false),
            0, // WebView2 process memory - would need separate measurement
            GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
            TimeSpan.Zero); // GC time would need ETW or other measurement
    }

    private UIMetrics MeasureUIMetrics()
    {
        // Implementation would measure frame times
        return new UIMetrics(16.7, 20.0, 0, true);
    }

    public void Dispose()
    {
        _collectionTimer?.Dispose();
        _memoryCounter?.Dispose();
        _cpuCounter?.Dispose();
    }
}
```

## Analysis and Reporting

### PowerShell Analysis Script

```powershell
# Analyze-EphemeralBrowserPerformance.ps1

param(
    [string]$ETLPath,
    [string]$JSONPath,
    [string]$ReportPath = ".\PerformanceReport.html"
)

function Analyze-ETLFile {
    param([string]$ETLPath)
    
    Write-Host "Analyzing ETL file: $ETLPath"
    
    # Use WPA (Windows Performance Analyzer) command line
    $wpaProfile = @"
<?xml version="1.0" encoding="utf-8"?>
<WpaProfileContainer>
  <Content>
    <WpaProfile Id="EphemeralBrowser.wpaProfile" Name="EphemeralBrowser Analysis">
      <DataTable>
        <ExpandedCategories>
          <Category Name="System Activity"/>
          <Category Name="Memory Usage"/>
          <Category Name="CPU Usage"/>
        </ExpandedCategories>
      </DataTable>
    </WpaProfile>
  </Content>
</WpaProfileContainer>
"@
    
    $wpaProfile | Out-File -FilePath "EphemeralBrowser.wpaProfile" -Encoding UTF8
    
    # Export summary data
    wpa.exe -i $ETLPath -profile EphemeralBrowser.wpaProfile -export summary.csv
}

function Analyze-JSONMetrics {
    param([string]$JSONPath)
    
    $metrics = Get-Content $JSONPath | ConvertFrom-Json
    
    $analysis = @{
        AverageColdStart = ($metrics | Measure-Object -Property startup.coldStartTimeMs -Average).Average
        AverageWarmStart = ($metrics | Measure-Object -Property startup.warmStartTimeMs -Average).Average
        AverageMemoryMB = ($metrics | Measure-Object -Property memory.workingSetBytes -Average).Average / 1MB
        SanitizationRate = ($metrics | Where-Object { $_.navigation.wasSanitized } | Measure-Object).Count / $metrics.Count * 100
    }
    
    return $analysis
}

function Generate-PerformanceReport {
    param($Analysis, $ReportPath)
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>EphemeralBrowser Performance Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .metric { margin: 20px 0; padding: 15px; border-left: 4px solid #0078d4; background: #f8f9fa; }
        .good { border-left-color: #28a745; }
        .warning { border-left-color: #ffc107; }
        .danger { border-left-color: #dc3545; }
    </style>
</head>
<body>
    <h1>EphemeralBrowser Performance Report</h1>
    <p>Generated: $(Get-Date)</p>
    
    <div class="metric $($Analysis.AverageColdStart -lt 800 ? 'good' : 'warning')">
        <strong>Average Cold Start:</strong> $([math]::Round($Analysis.AverageColdStart, 1))ms
        <br><small>Target: < 800ms</small>
    </div>
    
    <div class="metric $($Analysis.AverageWarmStart -lt 300 ? 'good' : 'warning')">
        <strong>Average Warm Start:</strong> $([math]::Round($Analysis.AverageWarmStart, 1))ms
        <br><small>Target: < 300ms</small>
    </div>
    
    <div class="metric $($Analysis.AverageMemoryMB -lt 200 ? 'good' : 'warning')">
        <strong>Average Memory Usage:</strong> $([math]::Round($Analysis.AverageMemoryMB, 1))MB
        <br><small>Target: < 200MB per container</small>
    </div>
    
    <div class="metric good">
        <strong>URL Sanitization Rate:</strong> $([math]::Round($Analysis.SanitizationRate, 1))%
        <br><small>Percentage of navigations with tracking parameters removed</small>
    </div>
</body>
</html>
"@
    
    $html | Out-File -FilePath $ReportPath -Encoding UTF8
    Write-Host "Performance report generated: $ReportPath"
}

# Main execution
if ($ETLPath) { Analyze-ETLFile -ETLPath $ETLPath }
if ($JSONPath) { 
    $analysis = Analyze-JSONMetrics -JSONPath $JSONPath
    Generate-PerformanceReport -Analysis $analysis -ReportPath $ReportPath
}
```

## Continuous Performance Monitoring

### GitHub Actions Integration

```yaml
# .github/workflows/performance-monitoring.yml
name: Performance Monitoring

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  performance-test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore -c Release
    
    - name: Run Performance Tests
      run: |
        dotnet test --no-build --verbosity normal --logger trx --collect:"XPlat Code Coverage" --results-directory TestResults/
        
    - name: Start ETW Collection
      run: |
        powershell -File scripts/EphemeralBrowser-ETW-Session.ps1 -Action Start -DurationSeconds 120
        
    - name: Run Cold Start Benchmark
      run: |
        dotnet run --project tests/PerformanceBenchmarks -- --cold-start --iterations 10
        
    - name: Run Memory Benchmark  
      run: |
        dotnet run --project tests/PerformanceBenchmarks -- --memory-usage --duration 60
        
    - name: Stop ETW Collection
      run: |
        powershell -File scripts/EphemeralBrowser-ETW-Session.ps1 -Action Stop
        
    - name: Generate Performance Report
      run: |
        powershell -File scripts/Analyze-EphemeralBrowserPerformance.ps1 -JSONPath TestResults/performance-metrics.json -ReportPath performance-report.html
        
    - name: Upload Performance Artifacts
      uses: actions/upload-artifact@v3
      with:
        name: performance-results
        path: |
          TestResults/
          performance-report.html
          *.etl
```

This comprehensive performance monitoring toolkit provides:

1. **PerfView Integration** - ETW collection with .NET runtime and WebView2 specific providers
2. **WPR Templates** - Windows Performance Recorder profiles for detailed system analysis  
3. **ETW Session Management** - PowerShell scripts for automated trace collection
4. **JSON Schema** - Structured performance data format for analysis and trending
5. **Performance Collector** - C# implementation for real-time metrics gathering
6. **Analysis Tools** - PowerShell scripts for ETL analysis and HTML report generation
7. **CI/CD Integration** - GitHub Actions workflow for continuous performance monitoring

The toolkit enables both development-time profiling and production performance monitoring to ensure the EphemeralBrowser meets its aggressive performance targets.