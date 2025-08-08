# EphemeralBrowser

A privacy-first ephemeral browser built with **WPF/.NET 8** and **WebView2**. Each browsing session runs in an isolated, temporary container that's completely wiped after use.

## 🚀 **Quick Start**

### Prerequisites
- **.NET 8 SDK** or later
- **Windows 10/11** (version 1803 or later)
- **WebView2 Runtime** (see installation instructions below)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)

### Dependency Setup

#### 1. Install .NET 8 SDK
```bash
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Or via winget:
winget install Microsoft.DotNet.SDK.8

# Verify installation:
dotnet --version
```

#### 2. WebView2 Runtime (Required)
The application requires Microsoft Edge WebView2 Runtime to be installed:

```bash
# Option 1: Download manually
# Visit: https://developer.microsoft.com/en-us/microsoft-edge/webview2/
# Download the "Evergreen Standalone Installer"

# Option 2: Install via winget (recommended)
winget install Microsoft.EdgeWebView2Runtime

# Option 3: Install via PowerShell (requires admin)
$url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
$output = "$env:TEMP\MicrosoftEdgeWebview2Setup.exe"
Invoke-WebRequest -Uri $url -OutFile $output
Start-Process -FilePath $output -ArgumentList '/silent' -Wait
```

**Verify WebView2 installation:**
```bash
# Check if installed via PowerShell:
Get-AppxPackage -Name "*WebView2*" | Select-Object Name, Version

# Or check registry:
Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -Name pv
```

#### 3. WPF Workload (if needed)
```bash
# Install WPF workload for .NET 8:
dotnet workload install microsoft-windows-desktop
```

### Build & Run

```bash
# 1. Clone and navigate to project
git clone https://github.com/CJCLAASSEN98/privacy_browser.git
cd privacy_browser

# 2. Restore dependencies
dotnet restore

# 3. Build solution
dotnet build

# 4. Run in development
dotnet run --project src/EphemeralBrowser.App

# 5. Build optimized single-file executable (creates desktop shortcut)
dotnet publish src/EphemeralBrowser.App/EphemeralBrowser.App.csproj -c Release -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=true

# 6. Run the built application
cd src/EphemeralBrowser.App/bin/Release/net8.0-windows/win-x64/publish
./EphemeralBrowser.App.exe
```

### Development Workflow

```bash
# Run tests
dotnet test

# Run with specific test category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration

# Run benchmarks
dotnet run --project tests/EphemeralBrowser.Benchmarks -c Release

# Format code
dotnet format

# Analyze code quality
dotnet build --verbosity normal
```

## 🏗️ **Architecture**

**Pattern**: MVVM with dependency injection  
**Threading**: Async/await with UI thread marshalling  
**Performance**: <800ms cold start, <150ms sanitizer overhead  

### Project Structure

```
src/
├── EphemeralBrowser.App/     # WPF application entry point
├── EphemeralBrowser.Core/    # Core services & business logic
├── EphemeralBrowser.UI/      # ViewModels, Views, Converters
tests/
├── EphemeralBrowser.Tests.Unit/        # xUnit unit tests
├── EphemeralBrowser.Tests.Integration/ # Playwright integration tests
├── EphemeralBrowser.Benchmarks/       # Performance benchmarks
examples/
├── backend-privacy/          # Privacy service examples
└── frontend-ux/             # UI component examples
```

## 🔒 **Privacy Features**

- **Ephemeral Profiles**: Each tab gets a unique temporary user-data folder, completely wiped on close
- **URL Sanitization**: Removes tracking parameters (utm_*, fbclid, gclid, etc.)
- **Anti-Fingerprinting Shims**: Document-start injection to reduce browser fingerprinting
- **Download Quarantine**: SHA-256 verification and Mark-of-the-Web (MOTW) tagging
- **No Persistence**: Complete session isolation with secure data wiping

## 🛠️ **Key Components**

### UrlSanitizer Service
- Real-time removal of tracking parameters during navigation
- Domain-specific allow/block rules
- Performance monitoring (<150ms processing requirement)
- Configurable via JSON rules

### Profile Manager
- Creates unique temporary browser profiles per tab
- Automatic cleanup on session end
- Secure deletion of sensitive data

### Download Gate
- Quarantines downloads by default
- Computes SHA-256 hashes for verification
- Applies Mark-of-the-Web for Windows security integration

## 🔧 **Configuration**

The browser supports configurable rules for URL sanitization and anti-fingerprinting settings. See the `examples/` directory for sample configurations.

## 📊 **Performance Targets**

- **Cold start**: <800ms
- **Warm start**: <300ms  
- **URL sanitization**: <150ms overhead
- **Memory usage**: <200MB per container
- **Profile teardown**: <500ms

## 🧪 **Testing**

Run the complete test suite:

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter Category=Unit

# Integration tests (requires WebView2)
dotnet test --filter Category=Integration

# Performance benchmarks
dotnet run --project tests/EphemeralBrowser.Benchmarks -c Release
```

## 📝 **Development**

This project follows strict architectural guidelines:
- **MVVM pattern** with CommunityToolkit.Mvvm
- **Dependency injection** via Microsoft.Extensions.DI
- **Async-first** design with proper UI thread marshalling
- **Performance monitoring** with ETW and JSON logs
- **Code quality**: Nullable reference types, StyleCop, .NET analyzers


## 📦 **Distribution**

### Desktop Shortcut Creation
The Release build automatically creates a desktop shortcut (`EphemeralBrowser.lnk`) using a temporary PowerShell script. The shortcut includes:
- Proper target path to the published executable
- Correct working directory 
- Application icon and description
- Fail-safe execution (build succeeds even if shortcut creation fails)

### Packaging Options
- **MSIX** (preferred): Code-signed modern Windows app package
- **Portable**: Single-file executable (current default)
- **WiX/MSI**: Traditional installer (fallback option)

## ⚠️ **Troubleshooting**

### WebView2 Runtime Issues
- **App won't start**: Ensure WebView2 Runtime is installed (see dependency setup above)
- **Windows 11**: Runtime is usually pre-installed
- **Windows 10**: Manual installation typically required
- **Version compatibility**: App uses WebView2 version 1.0.2277.86

### Build Issues
- **Framework targeting**: Ensure you're targeting `net8.0-windows`
- **WPF workload**: Verify installed with `dotnet workload install microsoft-windows-desktop`
- **SDK version**: Check with `dotnet --version` (must be 8.0 or later)

### WPF + IL Trimming Incompatibility
**Always explicitly disable trimming** for WPF applications by including `-p:PublishTrimmed=false` in your publish command. WPF relies heavily on reflection for XAML binding and will break with IL trimming enabled. Use `PublishReadyToRun=true` instead for better performance without compatibility issues.

## 🏷️ **Versioning**

This project uses semantic versioning. See the changelog for release notes and breaking changes.

## 📄 **License**

Commercial license - see LICENSE file for details. Contact claassen.cjs@gmail.com for commercial licensing.
