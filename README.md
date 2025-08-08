# EphemeralBrowser

A privacy-first ephemeral browser built with **WPF/.NET 8** and **WebView2**. Each browsing session runs in an isolated, temporary container that's completely wiped after use.

## 🚀 **Quick Start**

### Prerequisites
- **.NET 8 SDK** or later
- **Windows 10/11** (WebView2 supported)
- **WebView2 Runtime** (automatically bootstrapped if missing)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)

### Build & Run

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run in development
dotnet run --project src/EphemeralBrowser.App

# Build optimized single-file executable (creates desktop shortcut)
dotnet publish src/EphemeralBrowser.App/EphemeralBrowser.App.csproj -c Release -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=true

# Run the built application
cd src/EphemeralBrowser.App/bin/Release/net8.0-windows/win-x64/publish
./EphemeralBrowser.App.exe
```

### Development

```bash
# Run tests
dotnet test

# Run with specific test category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration

# Run benchmarks
dotnet run --project tests/EphemeralBrowser.Benchmarks -c Release
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

See `CLAUDE.md` for detailed development guidelines and coding standards.

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

## ⚠️ **Known Issues**

### WPF + IL Trimming Incompatibility
**Always explicitly disable trimming** for WPF applications by including `-p:PublishTrimmed=false` in your publish command. WPF relies heavily on reflection for XAML binding and will break with IL trimming enabled. Use `PublishReadyToRun=true` instead for better performance without compatibility issues.

## 🏷️ **Versioning**

This project uses semantic versioning. See the changelog for release notes and breaking changes.

## 📄 **License & Commercial Use**

**EphemeralBrowser Commercial License**

Copyright © 2025 EphemeralBrowser. All Rights Reserved.

### ⚠️ Commercial Use Prohibited Without License

This software and associated documentation files (the "Software") are proprietary and confidential. **Commercial use, distribution, or profit from this Software is strictly prohibited** without explicit written permission and appropriate licensing fees.

### ✅ Permitted Uses (Non-Commercial Only)
- Personal, non-commercial use
- Educational and research purposes  
- Evaluation for potential licensing

### ❌ Prohibited Without Commercial License
- Any commercial use or deployment in business environments
- Redistribution for profit or commercial gain
- Integration into commercial products or services
- Use by businesses, organizations, or entities for operational purposes
- Modification and redistribution without explicit permission
- Reverse engineering for competitive purposes

### 💰 Commercial Licensing Required
For commercial use, enterprise deployment, or revenue-generating applications, **you must obtain a commercial license and pay licensing fees**. Commercial licenses are available on a per-deployment, per-user, or revenue-sharing basis.

**Licensing Contact**: [Add your contact information for licensing inquiries]

### ⚖️ Legal Enforcement
**Violation of this license will result in immediate legal action.** Unauthorized commercial use subjects violators to liability for:
- Lost licensing revenue and profits
- Legal fees and court costs  
- Statutory damages up to $150,000 per work
- Injunctive relief to stop unauthorized use

### 🛡️ Warranty Disclaimer
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY.