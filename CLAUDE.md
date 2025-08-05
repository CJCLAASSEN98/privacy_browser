### 🔄 Project Awareness & Context
- **Always read `PLANNING.md`** at the start of a new conversation to understand the project's architecture, goals, style, and constraints (**Windows WPF/.NET 8, WebView2, MVVM**).
- **Check `TASK.md`** before starting a new task. If the task isn't listed, add it with a brief description and today's date.
- **Reference `INITIAL.md`** for the complete feature specification of the **Ephemeral Containers for WebView2** application.
- **Use consistent naming conventions, file structure, and architecture patterns** as described in `PLANNING.md` (MVVM, services, and interop layers).
- **Always sync your TODOs with `TASK.md`** (add a "Discovered During Work" section entries as they arise).

### 🧱 Code Structure & Modularity
- **Never create a file longer than ~500 lines.** If a file approaches this limit, refactor by splitting into modules or helper classes.
- **Solution layout (suggested):**
  - `src/AppShell/` – WPF UI project (Views, ViewModels, Commands, Resources)
    - `App.xaml/.cs` – Application startup & DI bootstrapping
    - `MainWindow.xaml/.cs` – Shell window; tab host and panels
    - `Views/` – `PrivacyPanel.xaml`, `RulesPanel.xaml`, `DiagnosticsPanel.xaml`, `DownloadsPane.xaml`
    - `ViewModels/` – `MainViewModel.cs`, `EphemeralProfileViewModel.cs`, `DownloadItemViewModel.cs`
    - `Converters/`, `Behaviors/`, `Controls/` – UI utilities and custom controls
  - `src/BrowserCore/` – WebView2 hosting & navigation
    - `WebViewHost.cs` – wrapper for lifecycle and events
    - `UrlSanitizer/UrlSanitizer.cs` – rules engine and safe-redirect logic
    - `Shims/ScriptInjector.cs` – document-start injection APIs
    - `Profiles/ProfileManager.cs` – temp user-data folders & teardown
    - `Downloads/DownloadGate.cs` – quarantine, SHA-256, MOTW tagging
  - `src/Security/` – security helpers (MOTW interop, hashing, secure delete, path validation)
  - `src/Perf/` – ETW/PerfView instrumentation & JSON perf logs
  - `src/Interop/` – COM/Win32 interop (e.g., `IAttachmentExecute`), helpers, P/Invoke
  - `tests/` – xUnit + Playwright for .NET + integration tests
  - `tools/` – rules packer, shim bundler/minifier
- **Project & dependency management:** SDK-style `.csproj` with `Directory.Build.props/targets` and `Directory.Packages.props` (central package management).
- **Includes & dependencies:** keep **clear, minimal references** per project; expose abstractions via interfaces in `BrowserCore` to reduce UI coupling.

### 🧾 Naming & Conventions (.NET)
- **Types & members:** `PascalCase`. **Fields:** `_camelCase`. **Parameters/local:** `camelCase`. **Interfaces:** `I*` (e.g., `IProfileManager`). **Events:** `EventName` with `EventArgs`.
- **Nullability:** `#nullable enable` in all projects; **treat warnings as errors** (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **Analyzers:** Enable .NET analyzers + StyleCop; use an `.editorconfig` aligned with .NET and WPF/XAML conventions.

### 🧪 Testing & Reliability
- **Create xUnit tests** for new features in `tests/UnitTests/`.
  - Provide: **1 expected-use**, **1 edge-case**, **1 failure/exception** test minimum.
- **Web content & end-to-end:** **Playwright for .NET** (`tests/E2E/`) to automate navigation, sanitizer behavior, download gating, and verify **no persistence** between sessions.
- **WPF UI components:** use **FlaUI** or **WPF-UI Testing** where applicable for basic interaction; keep tests fast and isolated.
- **After updating any logic**, review & update affected tests. Keep CI green.
- **Test data:** store golden files for sanitizer rules and shim bundles.

### ✅ Task Completion
- **Mark completed tasks in `TASK.md`** immediately after finishing.
- Add new sub-tasks/TODOs discovered during development under **"Discovered During Work"** in `TASK.md`.

### 📎 Style & Conventions
- **Framework:** **.NET 8, WPF, WebView2, MVVM** (CommunityToolkit.Mvvm).
- **DI/Logging:** `Microsoft.Extensions.DependencyInjection` & `Microsoft.Extensions.Logging` with structured logs.
- **Async:** Prefer `async`/`await`; **never block the UI thread**. Use `IProgress<T>` or dispatcher when updating UI.
- **Code quality:** RAII via `IDisposable/IAsyncDisposable`; `using` declarations; `readonly` where possible; immutability for models.
- **Formatting:** `dotnet format` + `.editorconfig`; consistent XAML style (attached properties first, then layout, then bindings).

### 🖥️ WebView2, Profiles & Downloads
- **Ephemeral profiles:** create a **unique temp user-data folder** per tab; wipe on close with ordered disposal.
- **URL Sanitization:** rewrite on `NavigationStarting`; if modified, **cancel & re-navigate**; show a "Sanitized" UI chip with **undo**.
- **Document-start shims:** inject anti-fingerprinting bundle (canvas/audio noise, timer quantization); **per-site toggles** with a safelist for breakage.
- **Downloads:** quarantine by default; compute **SHA-256**; tag with **MOTW** using `IAttachmentExecute`; allow **Promote** (move and keep) vs **Delete on close**.

### 🎨 UI/UX Design Principles
- **WPF Widgets:** native look; high DPI aware; theming via `ResourceDictionary` (Light/Dark).
- **Virtualization:** use `VirtualizingStackPanel`, `EnableRowVirtualization`, and `EnableColumnVirtualization` for large logs/lists.
- **Responsiveness:** keep UI thread under **16ms/frame**; offload I/O and hashing.
- **Keyboard shortcuts:** omnibox focus, new container, toggle sanitizer/shims, promote download.
- **Accessibility:** UIA patterns, tab order, focus visuals, high-contrast themes.

### 🔒 Security & File Safety
- **MOTW:** apply Mark-of-the-Web to downloads; surface SmartScreen results.
- **Ephemeral teardown:** guarantee disposal → process exit wait → **secure wipe** (best-effort for small files).
- **Path validation:** prevent directory traversal; restrict promotes to user-approved locations.
- **Crypto:** use `SHA256.Create()`; store hashes only in memory unless explicitly exported by user.
- **Privacy:** no network telemetry; perf logs are **local-only** and opt-in to export.

### 🚀 Performance Optimization
- **Perf budgets (mid-tier laptop):**
  - Cold start **< 800 ms**, warm **< 300 ms**
  - First-nav sanitizer overhead **< 150 ms**
  - Steady-state memory **< 200 MB** per container
  - Teardown (profile wipe) **< 500 ms**
- **Tooling:** ETW + PerfView; Windows Performance Recorder (I/O & CPU); JSON perf logs (TTI, memory peak/steady).
- **Practices:** avoid sync-over-async; minimize allocations in hot paths; reuse buffers; cache compiled regex; pool `HttpClient` if used.

### 📦 Packaging & Distribution
- **MSIX** (preferred) with code signing; optional **WiX/MSI** fallback.
- **WebView2 Runtime:** detect & bootstrap evergreen runtime when missing.
- **winget** manifest for distribution; upload PDB/symbols for crash analysis.
- **CI:** GitHub Actions (build, test, package). Artifact: MSIX + zipped portable build.

### 📚 Documentation & Explainability
- **Update `README.md`** whenever features, dependencies, or build steps change.
- **XML documentation comments** for public APIs; consider **DocFX** for site generation.
- **Inline comments** explain **why**, not just what—especially in sanitizer rules and shims.
- **Perf docs:** include benchmarks and rationale in `performance-monitoring.md`.

### 🔧 Build Script Maintenance
- **When adding NuGet packages**, update `Directory.Packages.props` (central package versions).
- **Update `.csproj`** to include new analyzers/tools and set `<Nullable>enable</Nullable>`.
- **Interop additions:** document any new COM/PInvoke in `src/Interop/README.md` and add tests.
- **Cross-machine builds:** validate CI on Windows Server and local Windows 11 dev boxes.
- **After dependency changes,** run full CI matrix and update `PLANNING.md` prerequisites.
- **Document runtime requirements** (WebView2 runtime, VC++ redist if needed) in installer notes.

### 🧠 AI Behavior Rules
- **Never assume missing context. Ask questions if uncertain.**
- **Never hallucinate .NET/WPF/WebView2 APIs** – only use documented APIs (link docs in PRs).
- **Confirm namespaces & package references** before using in code.
- **Never delete or overwrite existing code** unless explicitly instructed, or if listed in `TASK.md`.
- **Use modern C#/.NET 8 features** responsibly (pattern matching, records/classes, spans where safe).
- **Consider thread safety** in event handlers and async flows; marshal back to UI thread via `Dispatcher`.
