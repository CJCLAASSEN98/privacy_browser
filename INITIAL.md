Ephemeral Containers for WebView2 (Windows) – Key Features

🧭 Browser Shell & Tab Model
- WPF/.NET 8 shell with MVVM; tabs bound to EphemeralProfileViewModel
- Per-site, per-session profiles (unique temp user-data folder per tab/container)
- Startup Boost: pre-create WebView2 environment; defer non-critical UI (menus/telemetry)
- Keyboard-first navigation (omnibox focus, new container, sanitize-toggle hotkeys)

🔒 Privacy & Data Minimization
- URL Tracking-Param Sanitizer at NavigationStarting
  Strips utm_*, fbclid, gclid, mc_eid, igshid, etc.; safe-redirect (cancel + re-navigate)
  Visual “Sanitized” chip + one-click undo
- Anti-Fingerprinting Shims (document-start injection)
  Canvas/audio noise, timer quantization, disable Battery API; per-site toggles
- Ephemeral Storage
  Cookies, cache, localStorage scoped to the temp container; wiped on close

⬇️ Download Gate & MOTW
- Quarantine-by-default: downloads land in container temp dir
- Mark-of-the-Web via IAttachmentExecute.Save() (COM) to enable SmartScreen/AV
- File card shows origin, MIME, size, SHA-256; actions: Promote (move + keep) / Delete on close
- Content-type allowlist & extension mismatch warnings

🧰 Controls & Panels
- Privacy Panel: per-site policy (Strict / Balanced / Trusted)
- Rules Panel: view/override sanitizer params; load signed rules pack
- Diagnostics Panel: last navigation events, injected shims, download MOTW status

📊 Performance & UX
- Target Budgets (mid-tier laptop)
  - Cold start < 800 ms; warm start < 300 ms
  - First-nav sanitizer overhead < 150 ms
  - Steady-state memory < 200 MB/container on top-10 sites
  - Teardown < 500 ms (profile wipe + handle release)
- Instrumentation: ETW/PerfView + app-local JSON perf logs
- UI Responsiveness: 60 fps goal; async command pipeline (I/O off UI thread)

🛡️ Security & Integrity
- Secure Teardown: dispose WebView, recursively wipe temp profile (best-effort overwrite for small files)
- Transactional Moves: promote/download operations are atomic with rollback on failure
- Local-only Telemetry: perf stats cached to %LOCALAPPDATA%\<app>\Perf\ (opt-in export)
- Background Update (optional): signed rules bundle; pinned CA/cert thumbprint

🧪 Quality & Testing
- Unit: URL rules, profile lifecycle, shim switches, hashing/MOTW, secure delete
- Integration/UI: Playwright for .NET drives top domains; asserts storage is empty after close/reopen
- Security Checks: rule bypass tests, extension mismatch, SmartScreen/MOTW tagging assertions
- Static Analysis: .NET analyzers, StyleCop, Roslyn nullability enabled

📦 Packaging & Distribution (Windows-first)
- dotnet publish -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true
- MSIX with code signing; optional WiX/MSI fallback
- winget manifest; symbols/PDBs uploaded for crash analysis
- WebView2 Runtime detection (evergreen installer bootstrap)

— — —

EXAMPLES

The examples/ folder demonstrates high-performance, privacy-first patterns for .NET 8 + WPF + WebView2.

Backend/Privacy Examples (backend-privacy/)

1) UrlSanitizer.cs
- Rules engine (regex + allowlist) with unit tests & golden files
- Safe redirect: cancel & Navigate(); emits NavSanitized(original, sanitized)
- Metrics: per-domain strip rate, median added latency

2) ProfileManager.cs
- Temp user-data folder per container; leak-safe disposal
- Teardown sequencing: WebView dispose → process exit wait → directory wipe
- Failure-mode handling (retry with backoff; locked handle diagnostics)

3) DownloadGate.cs
- WebView2 download events → quarantine dir
- MOTW tagging via IAttachmentExecute (COM interop)
- SHA-256 streaming hash; Promote vs Delete on close

Frontend/UX Examples (frontend-ux/)

4) AntiFpShims.bundle.js
- Document-start injection (AddScriptToExecuteOnDocumentCreatedAsync)
- Canvas/audio noise; performance.now quantization; feature flags
- Per-site override & breakage safelist

5) TabsAndPanels.xaml/.cs
- MVVM with ICommand async actions; UI virtualization for history/logs
- Non-blocking status toasts; “Sanitized” chip; privacy grade indicator

Performance Monitoring (performance-monitoring.md)

6) Complete profiling toolkit
- PerfView preset (GC, allocations, file I/O) + ETW session recipe
- Windows Performance Recorder template for navigation spikes
- JSON perf schema (TTI, cold/warm start, memory peak/steady)

Key Performance Patterns Demonstrated

| Area   | Pattern                                      | Gain                     |
|--------|----------------------------------------------|--------------------------|
| Startup| Pre-init WebView2 + deferred UI              | Sub-second cold start    |
| Nav    | Param stripping in NavigationStarting         | <150 ms overhead         |
| Memory | Ephemeral profiles + GC pressure controls     | <200 MB steady/container |
| Teardown| Ordered dispose + wipe                       | <500 ms close latency    |

— — —

DOCUMENTATION

Core Framework
- .NET 8: https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8
- WPF: https://learn.microsoft.com/dotnet/desktop/wpf/
- MVVM (CommunityToolkit.Mvvm): https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/
- WebView2: https://learn.microsoft.com/microsoft-edge/webview2/

WebView2 & Platform APIs
- AddScriptToExecuteOnDocumentCreatedAsync:
  https://learn.microsoft.com/microsoft-edge/webview2/reference/win32/icorewebview2#addscripttoexecuteondocumentcreatedasync
- Navigation events:
  https://learn.microsoft.com/microsoft-edge/webview2/reference/win32/icorewebview2navigationstartingeventargs
- Downloads:
  https://learn.microsoft.com/microsoft-edge/webview2/reference/win32/icorewebview2downloadoperation
- Attachment Execute / MOTW:
  https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-iattachmentexecute

Testing & Quality
- xUnit: https://xunit.net/
- Playwright for .NET: https://playwright.dev/dotnet/
- .NET Analyzers: https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview
- StyleCop Analyzers: https://github.com/DotNetAnalyzers/StyleCopAnalyzers

Dev Tools
- Visual Studio 2022: https://visualstudio.microsoft.com/
- dotnet CLI: https://learn.microsoft.com/dotnet/core/tools/
- PerfView: https://github.com/microsoft/perfview
- Windows Performance Toolkit: https://learn.microsoft.com/windows-hardware/test/wpt/

CI/CD & Packaging
- GitHub Actions for .NET: https://github.com/actions/setup-dotnet
- MSIX Packaging: https://learn.microsoft.com/windows/msix/
- WiX Toolset (MSI): https://wixtoolset.org/
- winget: https://learn.microsoft.com/windows/package-manager/

Security & Best Practices
- STRIDE Threat Modeling: https://learn.microsoft.com/azure/security/develop/threat-modeling-tool
- OWASP ASVS: https://owasp.org/www-project-application-security-verification-standard/
- Windows Defender SmartScreen: https://learn.microsoft.com/microsoft-365/security/defender-endpoint/smartscreen-overview

Community & Support
- WebView2 Samples: https://github.com/MicrosoftEdge/WebView2Samples
- WPF Discussions: https://github.com/dotnet/wpf/discussions
- Rx.NET: https://github.com/dotnet/reactive

— — —

OTHER CONSIDERATIONS
- Privacy defaults: telemetry off; all perf logs local-only; explicit “Export Logs” action
- Rules provenance: signed sanitizer bundle; pin issuer & thumbprint; fail-closed on tamper
- Breakage management: per-site FP shim toggle and temporary allowlist
- Accessibility: UIA patterns; keyboard navigation; high-contrast themes
- Localization: resx-based; plan for RTL and pluralization
- Crash analysis: WER + symbol server; breadcrumbs redact URLs/params
- Future extensions:
  - Per-site persistent profiles (opt-in)
  - Rules auto-update channel with delta packs
  - Basic bookmark import → sanitized export (CSV/JSON)
  - Site-isolation sandbox (multiple processes) when feasible

— — —

Quickstart (Actionable)
Build (Release, single file)
  dotnet publish src/AppShell/AppShell.csproj -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true

Run
  ./src/AppShell/bin/Release/net8.0/win-x64/publish/AppShell.exe

Tests
  dotnet test

Demo path (5–7 min):
1) Launch (show sub-second cold start)
2) Navigate with tracking params (chip + undo)
3) Document-start shim toggle (see feature breakage and recovery)
4) Download file (MOTW, SHA-256)
5) Close tab (profile dir and quarantine wiped)
6) Export local perf logs
