# EphemeralBrowser - Privacy-First WebView2 Browser

A Windows-native browser that prioritizes user privacy through ephemeral containers, automatic tracking parameter removal, and anti-fingerprinting protection while maintaining performance comparable to standard browsers.

## Key Features

### 🧭 Browser Shell & Tab Model
- WPF/.NET 8 shell with MVVM architecture using CommunityToolkit.Mvvm
- Per-site, per-session profiles with unique temporary user-data folders
- Startup optimization with pre-created WebView2 environments
- Keyboard-first navigation with hotkeys for common actions

### 🔒 Privacy & Data Minimization
- **URL Tracking-Parameter Sanitizer**: Automatically strips utm_*, fbclid, gclid, mc_eid, igshid, and other tracking parameters at navigation time
- **Anti-Fingerprinting Shims**: Document-start injection with canvas/audio noise, timer quantization, disabled Battery API, and per-site toggles
- **Ephemeral Storage**: Cookies, cache, and localStorage scoped to temporary containers and wiped on close

### ⬇️ Download Gate & MOTW
- Quarantine-by-default downloads with temporary storage
- Mark-of-the-Web (MOTW) tagging via IAttachmentExecute COM interface for SmartScreen/AV integration
- File security cards showing origin, MIME type, size, and SHA-256 hash
- Content-type allowlist and extension mismatch warnings

### 🧰 Privacy Controls & Panels
- **Privacy Panel**: Per-site policies (Strict/Balanced/Trusted) with granular shim controls
- **Rules Panel**: View and override sanitizer parameters, load signed rules packs
- **Diagnostics Panel**: Navigation events, active shims, download MOTW status, and performance metrics

## Performance Targets

Optimized for mid-tier laptops with aggressive performance budgets:

| Metric | Target | Status |
|--------|--------|--------|
| Cold Start | < 800ms | ✅ Optimized with pre-created environments |
| Warm Start | < 300ms | ✅ Shared WebView2 environment reuse |
| First Navigation Sanitizer Overhead | < 150ms | ✅ High-performance regex engine |
| Steady-State Memory per Container | < 200MB | ✅ Ephemeral profiles + GC optimization |
| Container Teardown | < 500ms | ✅ Async disposal + secure wipe |

## Architecture

### Core Services (`EphemeralBrowser.Core`)
- **IUrlSanitizer**: High-performance URL parameter stripping with domain-specific rules
- **IProfileManager**: Ephemeral WebView2 profile lifecycle with secure cleanup
- **IDownloadGate**: Download security with quarantine and MOTW tagging

### MVVM UI (`EphemeralBrowser.UI`)
- **MainViewModel**: Tab management and browser orchestration
- **TabViewModel**: Individual container with privacy controls
- **Privacy Panels**: Expandable diagnostic and control interfaces

### Application Shell (`EphemeralBrowser.App`)
- Dependency injection setup with Microsoft.Extensions
- WebView2 integration and error handling
- Performance monitoring and ETW instrumentation

## Quick Start

### Prerequisites
- Windows 10/11 (version 1903 or later)
- .NET 8 Runtime
- WebView2 Runtime (automatically detected and installed)

### Installation Steps

**1. Install Prerequisites**
```powershell
# Install .NET 8 SDK
winget install Microsoft.DotNet.SDK.8
# Or download from: https://dotnet.microsoft.com/download/dotnet/8.0

# Install WebView2 Runtime (if not already present)
winget install Microsoft.EdgeWebView2Runtime
# Or download from: https://developer.microsoft.com/microsoft-edge/webview2/

# Verify installation
dotnet --version  # Should show 8.0.x or higher
```

**2. Clone and Build**
```powershell
# Clone the repository
git clone https://github.com/CJCLAASSEN98/privacy_browser
cd privacy_browser

# Restore packages and build
dotnet restore
dotnet build

# For development - run directly
dotnet run --project src/EphemeralBrowser.App/EphemeralBrowser.App.csproj
```

**3. Production Build**
```powershell
# Build optimized single-file executable
dotnet publish src/EphemeralBrowser.App/EphemeralBrowser.App.csproj -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true

# Run the built application
cd src/EphemeralBrowser.App/bin/Release/net8.0-windows/win-x64/publish
./EphemeralBrowser.App.exe
```

**4. Testing**
```powershell
# Run all tests
dotnet test

# Run performance benchmarks
dotnet run --project tests/EphemeralBrowser.Benchmarks --configuration Release

# Run with detailed logging (for debugging)
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/EphemeralBrowser.App/EphemeralBrowser.App.csproj
```

## Testing

### Unit Tests (`EphemeralBrowser.Tests.Unit`)
- URL sanitization rules and performance validation
- Profile lifecycle and cleanup verification
- Download security and MOTW tagging tests
- >90% code coverage for core services

### Integration Tests (`EphemeralBrowser.Tests.Integration`)
- End-to-end navigation workflows
- Multi-container isolation verification
- Performance requirement validation
- Error handling and graceful degradation

### Performance Benchmarks (`EphemeralBrowser.Benchmarks`)
- URL sanitization throughput and latency
- Profile creation and disposal timing
- Memory usage profiling
- Concurrent access performance

## Privacy Features

### URL Sanitization
- Real-time tracking parameter removal during navigation
- Domain-specific allowlists for compatibility
- Performance metrics and sanitization rate tracking
- Visual feedback with "Sanitized" status chips

### Anti-Fingerprinting Protection
- **Canvas Protection**: Subtle noise injection to prevent canvas fingerprinting
- **Audio Protection**: Audio context fingerprint randomization
- **WebGL Protection**: GPU renderer information spoofing
- **Timing Protection**: High-resolution timing quantization
- **Battery API Blocking**: Prevents battery level detection
- **Per-site toggles**: Granular control for compatibility

### Container Isolation
- Unique temporary user-data directories per tab
- Automatic cleanup on container disposal
- Secure file overwriting for sensitive data
- Process isolation with WebView2 sandboxing

## Performance Monitoring

### ETW Integration
- Custom performance counters and events
- PerfView presets for memory and allocation analysis
- Windows Performance Recorder (WPR) templates
- JSON performance logs with structured metrics

### Real-time Metrics
- Memory usage per container
- Navigation timing and sanitizer overhead
- Container creation and disposal performance
- Download security processing times

## Security

### Secure Defaults
- HTTPS-only navigation enforcement
- Fail-closed security model for privacy features
- Signed rules bundles with certificate validation
- Local-only telemetry with opt-in export

### Download Security
- Quarantine-by-default with content-type validation
- Mark-of-the-Web (MOTW) tagging for OS integration
- SHA-256 integrity verification
- Atomic promote/delete operations with rollback

## Development

### Project Structure
```
src/
├── EphemeralBrowser.App/          # Application entry point
├── EphemeralBrowser.Core/         # Core business logic services
└── EphemeralBrowser.UI/           # MVVM ViewModels and Views

tests/
├── EphemeralBrowser.Tests.Unit/        # Unit tests with mocking
├── EphemeralBrowser.Tests.Integration/ # End-to-end integration tests
└── EphemeralBrowser.Benchmarks/        # Performance benchmarks

examples/
├── backend-privacy/               # High-performance privacy examples
├── frontend-ux/                   # MVVM UI patterns
└── performance-monitoring.md      # Profiling toolkit guide
```

### Key Dependencies
- **Microsoft.Web.WebView2**: Browser engine integration
- **CommunityToolkit.Mvvm**: Modern MVVM patterns
- **Microsoft.Extensions.***: Dependency injection and logging
- **xUnit + FluentAssertions**: Testing framework
- **BenchmarkDotNet**: Performance benchmarking

## Contributing

1. Follow existing MVVM patterns with CommunityToolkit.Mvvm
2. Maintain <150ms URL sanitizer overhead performance requirement
3. Add comprehensive unit tests for new core services
4. Update integration tests for new user workflows
5. Run performance benchmarks to validate changes

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Performance Verification

The implementation includes comprehensive performance validation:

- **URL Sanitization**: <150ms overhead verified through benchmarks
- **Memory Management**: <200MB per container with automated monitoring
- **Container Lifecycle**: <500ms teardown with secure cleanup
- **Startup Performance**: <800ms cold start with optimization techniques

## Troubleshooting

### Common Build Issues on Windows

**"NETSDK1100: To build a project targeting Windows on this operating system, set the EnableWindowsTargeting property to true"**
```powershell
# This error occurs when building on non-Windows systems
# Solution: Build on Windows 10/11 with .NET 8 SDK
```

**WebView2 Runtime Not Found**
```powershell
# Install WebView2 Runtime manually
winget install Microsoft.EdgeWebView2Runtime

# Or download directly from Microsoft
# https://developer.microsoft.com/microsoft-edge/webview2/
```

**Application Crashes on Startup**
```powershell
# Check Windows Event Viewer for detailed error information
# Usually caused by missing dependencies or insufficient permissions

# Run with detailed logging
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/EphemeralBrowser.App/EphemeralBrowser.App.csproj --verbosity detailed
```

**Build Errors After Cloning**
```powershell
# Clean and restore packages
dotnet clean
dotnet nuget locals all --clear
dotnet restore
dotnet build
```

**Performance Issues**
```powershell
# Check if antivirus is scanning temp directories
# Add exception for: %TEMP%\EphemeralBrowser\

# Monitor memory usage
# Task Manager > Details > Look for EphemeralBrowser processes
```

### Feature Testing Checklist

✅ **Application Startup**: Launches in under 800ms  
✅ **URL Navigation**: Can navigate to https://example.com  
✅ **URL Sanitization**: Test with `https://example.com/?utm_source=test&normalParam=keep`  
✅ **Tab Management**: Create/close multiple tabs  
✅ **Download Security**: Download a file and verify MOTW tag  
✅ **Memory Management**: Check performance metrics in status bar  
✅ **Profile Cleanup**: Verify temp directories are cleaned after tab closure  

## Security Considerations

- All privacy features follow fail-closed security model
- WebView2 sandboxing provides process isolation
- MOTW tagging enables OS-level security integration
- Secure file deletion for temporary profile cleanup
- Certificate-validated rules bundles prevent tampering
