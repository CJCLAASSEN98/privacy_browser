# PRP: EphemeralBrowser - Privacy-First WebView2 Browser Implementation

## Project Context
**Why**: Create a Windows-native browser that prioritizes user privacy through ephemeral containers, automatic tracking parameter removal, and anti-fingerprinting protection while maintaining performance comparable to standard browsers.

**What**: Complete implementation of EphemeralBrowser with WPF/.NET 8 shell, per-site ephemeral profiles, URL sanitization, download security, anti-fingerprinting shims, and comprehensive performance monitoring. Target performance: <800ms cold start, <150ms sanitizer overhead, <200MB memory per container.

**Where**: New Windows desktop application integrating WebView2, WPF MVVM, privacy-focused backend services, and secure profile management. Uses existing examples in `/examples/` as implementation foundation.

## Codebase Context
**Architecture**: WPF/.NET 8 with MVVM pattern using CommunityToolkit.Mvvm; WebView2 for browser engine; ephemeral profile isolation; async command pipeline for UI responsiveness.

**Dependencies**: 
- .NET 8.0 (https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8)
- Microsoft.Web.WebView2 (latest)  
- CommunityToolkit.Mvvm 8.x (https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- Microsoft.Extensions.DependencyInjection
- System.Text.Json for configuration
- xUnit + Playwright.NET for testing

**Patterns**: Reference existing high-performance examples in `/examples/backend-privacy/` and `/examples/frontend-ux/`. Follow WebView2 samples architecture from https://github.com/MicrosoftEdge/WebView2Samples/tree/main/SampleApps/WebView2WpfBrowser.

**Tests**: xUnit for unit tests, Playwright for .NET for integration testing (https://playwright.dev/dotnet/), performance benchmarks with ETW collection.

## Research Findings
**Documentation**: 
- WebView2 Security Guide: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security
- User Data Folder Management: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder  
- WPF WebView2 Integration: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
- Navigation Events: https://learn.microsoft.com/microsoft-edge/webview2/reference/win32/icorewebview2navigationstartingeventargs
- MOTW Implementation: https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-iattachmentexecute

**Examples**: 
- Microsoft WebView2 WPF Browser sample: https://github.com/MicrosoftEdge/WebView2Samples/tree/main/SampleApps/WebView2WpfBrowser
- Existing privacy components in `/examples/backend-privacy/`: UrlSanitizer.cs, ProfileManager.cs, DownloadGate.cs
- UI patterns in `/examples/frontend-ux/`: TabsAndPanels.xaml/.cs, AntiFpShims.bundle.js

**Gotchas**: 
- Single-file deployment shows 2x slower initialization (342ms vs 196ms)
- WebView2 operations must be on UI thread; use Dispatcher.InvokeAsync for cross-thread calls
- Always remove event handlers before disposing environment objects to prevent memory leaks
- User data folders must be unique per container; use temporary directories with proper cleanup
- MOTW tagging requires elevated privileges or specific security context

**Best Practices**: 
- Pre-create WebView2 environment at startup for faster container creation
- Use CommunityToolkit.Mvvm [ObservableProperty] and [RelayCommand] for clean MVVM
- Implement IAsyncDisposable for proper resource cleanup
- Use structured logging with ETW for performance monitoring
- Follow fail-closed security model for privacy features

## Implementation Plan

### Pseudocode Approach
```
1. Application Startup:
   - Initialize shared WebView2 environment
   - Load privacy rules and configuration  
   - Create main window with tab management
   - Start performance monitoring

2. Container Creation:
   - Generate unique profile ID and temp directory
   - Create WebView2 with ephemeral user data folder
   - Inject anti-fingerprinting shims at document start
   - Configure privacy settings (disable autofill, tracking prevention)

3. Navigation Flow:
   - Intercept NavigationStarting event
   - Apply URL sanitization rules (strip tracking params)
   - Show sanitization status chip if params removed
   - Load page with privacy protections active

4. Download Security:
   - Intercept download events to quarantine directory
   - Apply MOTW tagging via IAttachmentExecute COM interface
   - Compute SHA-256 hash and show file card
   - Provide promote/delete actions

5. Container Teardown:
   - Dispose WebView2 and wait for process exit
   - Secure wipe profile directory with random overwrite
   - Clean up quarantine files
   - Update performance metrics
```

### File Structure
- `src/EphemeralBrowser.App/App.xaml/.cs`: Application entry point with DI setup
- `src/EphemeralBrowser.App/MainWindow.xaml/.cs`: Main browser shell UI
- `src/EphemeralBrowser.Core/Services/`: Core business logic services
  - `IProfileManager.cs`: Interface for ephemeral profile management
  - `ProfileManager.cs`: Implementation from `/examples/backend-privacy/ProfileManager.cs`
  - `IUrlSanitizer.cs`: Interface for URL tracking parameter removal  
  - `UrlSanitizer.cs`: Implementation from `/examples/backend-privacy/UrlSanitizer.cs`
  - `IDownloadGate.cs`: Interface for download security and MOTW
  - `DownloadGate.cs`: Implementation from `/examples/backend-privacy/DownloadGate.cs`
- `src/EphemeralBrowser.UI/ViewModels/`: MVVM view models
  - `MainViewModel.cs`: Main window and tab management
  - `TabViewModel.cs`: Individual container view model
  - `PrivacyPanelViewModel.cs`: Privacy settings panel
- `src/EphemeralBrowser.UI/Views/`: WPF views and user controls
  - `TabsAndPanels.xaml/.cs`: From `/examples/frontend-ux/TabsAndPanels.xaml/.cs`
- `src/EphemeralBrowser.UI/Resources/`: 
  - `AntiFpShims.bundle.js`: From `/examples/frontend-ux/AntiFpShims.bundle.js`
- `tests/EphemeralBrowser.Tests.Unit/`: xUnit unit tests
- `tests/EphemeralBrowser.Tests.Integration/`: Playwright integration tests
- `tests/EphemeralBrowser.Benchmarks/`: Performance benchmarks

### Integration Points
- WebView2 NavigationStarting event → UrlSanitizer service
- WebView2 DownloadStarting event → DownloadGate service  
- Document ready event → Anti-fingerprinting shim injection
- Container disposal → ProfileManager secure cleanup
- All operations → Performance monitoring and ETW logging

### Task Sequence
1. **Project Structure Setup**: Create solution with proper dependency injection and project references
2. **Core Services Implementation**: Integrate and adapt existing examples (UrlSanitizer, ProfileManager, DownloadGate)
3. **WebView2 Integration**: Main window with WebView2 hosting and shared environment
4. **MVVM ViewModels**: Tab management, privacy controls, and async command handling
5. **Privacy Features Integration**: URL sanitization, anti-fingerprinting shims, download security
6. **UI Implementation**: Tabs, status indicators, privacy panels using existing XAML patterns
7. **Performance Monitoring**: ETW integration and JSON metrics collection
8. **Testing Infrastructure**: Unit tests with mocking, Playwright integration tests
9. **Packaging and Distribution**: Single-file publish, MSIX packaging, installer creation

## Error Handling Strategy
**Common Failures**: 
- WebView2 runtime missing or incompatible version
- Profile directory access denied or in use by another process
- COM interface failures for MOTW tagging
- Network failures during navigation
- Memory pressure causing profile cleanup failures

**Validation**: 
- WebView2 availability check with graceful degradation
- Profile creation with retry logic and fallback directories
- Navigation validation with HTTPS enforcement
- Download validation with content-type and size limits
- Memory monitoring with automatic cleanup triggers

**Rollback**: 
- Failed profile creation → fallback to system temp directory
- Failed MOTW tagging → continue without SmartScreen integration
- Failed shim injection → continue with reduced privacy protection
- Failed secure delete → log warning and continue
- Navigation failures → show error page with retry option

## Validation Gates

### Syntax & Style
```bash
# .NET code analysis and formatting
dotnet format --verify-no-changes
dotnet build --configuration Release --verbosity minimal
```

### Unit Tests
```bash
# Run all unit tests with coverage
dotnet test tests/EphemeralBrowser.Tests.Unit/ --collect:"XPlat Code Coverage" --logger "console;verbosity=detailed"
```

### Integration Tests  
```bash
# Run Playwright integration tests
dotnet test tests/EphemeralBrowser.Tests.Integration/ --logger "console;verbosity=detailed"
```

### Performance Tests
```bash
# Run performance benchmarks
dotnet run --project tests/EphemeralBrowser.Benchmarks --configuration Release
```

### Security Validation
```bash
# Static analysis with security rules
dotnet run --project tools/SecurityAnalyzer -- --scan src/
```

## Anti-Patterns to Avoid
- **Blocking UI Thread**: Never perform WebView2 operations or file I/O on UI thread; use async/await patterns
- **Memory Leaks**: Always unsubscribe from WebView2 events before disposal; use weak event handlers where possible
- **Insecure Defaults**: Never allow mixed HTTP/HTTPS content; enforce HTTPS-only navigation
- **Profile Reuse**: Never reuse user data folders between containers; always use unique temporary directories
- **Synchronous Disposal**: Never use synchronous disposal for WebView2; implement IAsyncDisposable properly
- **Hardcoded Paths**: Never use hardcoded file paths; use Path.Combine and environment variables
- **Unsafe JSON Parsing**: Always validate and sanitize JSON configuration files and web messages
- **Process Leaks**: Always wait for WebView2 browser process exit before cleaning up profile directories

## Quality Checklist
- [ ] All INITIAL.md requirements implemented with performance targets met
- [ ] Follows existing code patterns from `/examples/` directory  
- [ ] Implements comprehensive error handling with graceful degradation
- [ ] Has unit tests with >90% coverage for core services
- [ ] Has integration tests covering complete user workflows
- [ ] Performance monitoring with ETW and JSON metrics implemented
- [ ] MVVM patterns using CommunityToolkit.Mvvm throughout
- [ ] Proper async/await usage with no blocking operations on UI thread
- [ ] Secure defaults with privacy-first configuration
- [ ] Memory leak prevention with proper event handler cleanup
- [ ] Cross-platform considerations documented (Windows-first, future Linux/Mac)

## Implementation Confidence
**Score**: 8/10 - High confidence in one-pass implementation success

**Reasoning**: 
- **Strengths**: Comprehensive examples already exist in `/examples/` directory providing proven implementation patterns; well-documented WebView2 APIs; clear MVVM architecture; established performance targets
- **Potential Issues**: WebView2 runtime compatibility across Windows versions; COM interop complexity for MOTW tagging; Performance optimization may require iterative tuning; Single-file deployment performance impact

**Risk Mitigation**: 
- Use existing working examples as foundation rather than starting from scratch
- Implement WebView2 runtime detection with graceful fallback
- Provide comprehensive error handling for COM interface failures  
- Performance benchmarking from day one with continuous monitoring
- Incremental implementation with working prototype at each stage

---
*Context is King: This PRP includes all necessary WebView2 patterns, existing working code examples, performance targets, security considerations, and validation approaches for successful one-pass implementation*