---
---

# CVOYA graph Build System Guide

This document explains the CVOYA graph build configurations, version management, and development workflows.

## 🚀 Quick Start

### Required Packages

To get started, install the Neo4j provider package (required):

```bash
dotnet add package Cvoya.Graph.Neo4j
```

Optionally, add the analyzers package for extra compile-time validation (recommended):

```bash
dotnet add package Cvoya.Graph.Analyzers
```

### Essential Commands

```bash
# Development build (fastest, project references)
dotnet build --configuration Debug

# Test package references locally
dotnet msbuild eng/PackageValidation.proj -target:Validate

# Create a release version
./scripts/create-release.sh -v 1.2.3

# Build production packages
dotnet build --configuration Release

# Run performance benchmarks
./scripts/run-benchmarks.sh

# Run local CodeQL analysis
./scripts/run-codeql.sh
```

## 📦 Build Configurations

CVOYA graph uses **four distinct build configurations** optimized for different scenarios:

| Configuration | Project Refs | Optimizations | Packages | VERSION Required | Use Case                |
| ------------- | ------------ | ------------- | -------- | ---------------- | ----------------------- |
| **Debug**     | ✅ Yes       | ❌ No         | ❌ No    | ❌ No            | Development & debugging |
| **Benchmark** | ✅ Yes       | ✅ Yes        | ❌ No    | ❌ No            | Performance testing     |
| **LocalFeed** | ✅ Yes       | ✅ Yes        | ✅ Yes   | ❌ No            | Local package testing   |
| **Release**   | ✅ Default   | ✅ Yes        | ✅ Yes   | ✅ Yes           | Production package builds |

### Configuration Details

#### Debug Configuration

- **Purpose**: Day-to-day development
- **Speed**: Fastest builds (no optimizations)
- **Dependencies**: Direct project references
- **Packages**: None generated
- **Usage**: `dotnet build --configuration Debug`

#### Benchmark Configuration

- **Purpose**: Performance testing and benchmarking
- **Speed**: Optimized builds with project references
- **Dependencies**: Direct project references (fast rebuilds)
- **Packages**: None generated
- **Usage**: `dotnet msbuild -target:BuildBenchmark`

#### LocalFeed Configuration

- **Purpose**: Building the local package set before package-reference validation
- **Speed**: Optimized builds with fast project references
- **Dependencies**: Direct project references (fast builds)
- **Packages**: Generated in the configured package output directory
- **Usage**: `dotnet build --configuration LocalFeed`

#### Release Configuration

- **Purpose**: Production builds and publishing
- **Speed**: Fully optimized
- **Dependencies**: Project references by default; the package-validation orchestrator explicitly sets `UsePackageReferences=true`
- **Packages**: Generated for publishing
- **Usage**: `dotnet build --configuration Release`

## 🏷️ Version Management

CVOYA graph uses a **VERSION file-based system** that ensures consistent versioning across all packages and prevents accidental releases.

### VERSION File Format

The `VERSION` file contains a single line with the version:

```text
# Stable release
1.2.3

# Pre-release
1.2.3-alpha
1.2.3-beta.1
1.2.3-rc.2
```

### Creating Releases

`scripts/create-release.sh` updates `VERSION` (and the separate `VERSION.ASSEMBLY`
stamp used for the numeric `AssemblyVersion`/`FileVersion`). It is the only thing
that writes those files — `Directory.Build.props` reads them at build time, so
nothing else needs to change and nothing gets stamped twice.

```bash
# Create a release version
./scripts/create-release.sh -v 1.2.3

# Create a pre-release version
./scripts/create-release.sh -v 1.2.3-alpha

# Preview without writing any files
./scripts/create-release.sh -v 1.2.3 --dry-run

# Update and commit in one step
./scripts/create-release.sh -v 1.2.3 --commit
```

See `./scripts/create-release.sh --help` for the full option list.

### Version Requirements

- **Debug/Benchmark**: No VERSION file needed (uses default version with timestamp)
- **Release**: VERSION file required for consistent package versions

### Publishing a Release

Publishing is entirely tag-triggered — there is no manual publish step:

1. Update `VERSION` with `./scripts/create-release.sh -v X.Y.Z --commit` (or edit
   `VERSION` directly and commit it).
2. Push a `vX.Y.Z` tag that matches the `VERSION` file content exactly:
   `git tag vX.Y.Z && git push origin vX.Y.Z`.
3. `.github/workflows/release.yml` verifies the tag matches `VERSION` (failing
   loudly on any mismatch), runs the full test suite including the Neo4j
   provider tests, packs all five packages, publishes to NuGet using **Trusted
   Publishing** (OIDC — no stored API key), attests build provenance, and
   creates the GitHub Release with generated notes.

See [docs/release-process.md](release-process.md) for the full process,
including the one-time NuGet Trusted Publishing portal setup.

## 🔄 Development Workflows

### Standard Development Workflow

```bash
# 1. Regular development (fastest)
dotnet build --configuration Debug

# 2. Run tests
dotnet test --configuration Debug

# 3. Performance testing when needed
dotnet build --configuration Benchmark
./scripts/run-benchmarks.sh
```

### Local CodeQL Analysis

CVOYA graph's GitHub workflow runs CodeQL for GitHub Actions, C#, and Ruby with
the `security-and-quality` query suite. To catch those findings before pushing,
install the CodeQL CLI and run:

```bash
./scripts/run-codeql.sh
```

The script writes one SARIF file per language under `artifacts/codeql/results/`.
It downloads the Actions, C#, and Ruby query packs by default so local scans use
the same query suite as `.github/workflows/codeql.yml`. The default CodeQL build
mode is `none`, which is the required portable local gate. In that mode, the
script analyzes a disposable source copy and temporary databases outside the
checkout so CodeQL dependency probing cannot rewrite repository files.

To trace the same `LocalFeed` and `Release` builds used by the GitHub workflow,
use manual build mode:

```bash
./scripts/run-codeql.sh --build-mode manual
```

Manual mode is optional because it relies on CodeQL compiler tracing support for
the local platform and .NET toolchain. If manual mode completes the build but
CodeQL exits with "could not process any" C# source, rerun the default command
without `--build-mode manual`. A successful default run satisfies the local
CodeQL gate; capture the manual-mode CodeQL version and `db/csharp/log` path in
PR notes only if the manual failure is relevant.

For a stricter local gate:

```bash
./scripts/run-codeql.sh --fail-on-alerts
```

To include CodeQL in the full build-system validation pass:

```bash
./scripts/validate-build.sh --codeql
```

### Release Preparation Workflow

```bash
# 1. Create release version
./scripts/create-release.sh -v 1.2.3

# 2. Verify release build works
dotnet build --configuration Release

# 3. Run full test suite
dotnet test --configuration Release

# 4. Commit and tag — pushing the tag triggers release.yml
git add VERSION VERSION.ASSEMBLY
git commit -m "chore: release 1.2.3"
git tag v1.2.3
git push origin v1.2.3
```

### Package Testing Workflow

Package-reference validation has one repository-level owner:

```bash
dotnet msbuild eng/PackageValidation.proj -target:Validate
```

The orchestrator starts from clean repository-owned state, restores and builds the `LocalFeed` package set, explicitly packs the analyzer projects, verifies all nine expected packages with `scripts/verify-package-set.sh`, and then restores/builds the solution with `UsePackageReferences=true`. `eng/package-validation.NuGet.config` maps `Cvoya.*` to the generated feed and everything else to NuGet.org.

All package, feed, global-packages, HTTP-cache, scratch, and plugin-cache paths live under `artifacts/package-validation/`. The workflow does not add or remove user-level NuGet sources and does not read, clear, or write the user's global NuGet caches.

The inventory check requires `bash` and `jq` on `PATH`; this is the same toolchain used by the repository scripts on Linux, macOS, and Git-for-Windows/GitHub-hosted Windows environments. All path construction and project classification inside MSBuild use OS-native normalized paths.

The compatibility wrappers delegate to the same orchestrator:

```bash
./scripts/setup-local-feed-msbuild.sh
./scripts/cleanup-local-feed.sh
```

## 🛠️ Available MSBuild Targets

### Version Management

```bash
# Create new release version
./scripts/create-release.sh -v X.Y.Z

# Show current version
dotnet msbuild -target:ShowVersion
```

### Build Commands

```bash
# Build specific configurations
dotnet build --configuration Debug
dotnet build --configuration Benchmark
dotnet build --configuration Release
```

### Local Package Testing

```bash
# Pack and verify the repository-scoped local feed
dotnet msbuild eng/PackageValidation.proj -target:PrepareLocalFeed

# Build the solution with locally produced package references
dotnet msbuild eng/PackageValidation.proj -target:BuildWithPackageReferences

# Run the complete required gate
dotnet msbuild eng/PackageValidation.proj -target:Validate

# Remove only repository-scoped validation state
dotnet msbuild eng/PackageValidation.proj -target:Clean
```

### Cleanup Commands

```bash
# Clean build outputs
dotnet clean
```

## 📁 Directory Structure

```text
graphmodel/
├── VERSION                    # Current release version (single source of truth)
├── VERSION.ASSEMBLY           # Numeric AssemblyVersion/FileVersion stamp
├── Directory.Build.props      # MSBuild configuration
├── eng/
│   ├── PackageValidation.proj        # Local package-validation orchestrator
│   └── package-validation.NuGet.config
├── artifacts/package-validation/     # Repository-scoped feed, packages, and caches
└── scripts/
    ├── create-release.sh     # Release creation helper
    ├── run-benchmarks.sh     # Benchmark runner
    └── cleanup-local-feed.sh # Legacy cleanup script
```

## 🚨 Error Prevention

### Common Errors and Solutions

#### "VERSION file is required for package generation"

```bash
# Solution: Create a release version first
./scripts/create-release.sh -v 1.2.3
```

#### "Package reference could not be resolved"

```bash
# Recreate the isolated feed and package-reference restore
dotnet msbuild eng/PackageValidation.proj -target:Validate
```

#### Build cache issues

```bash
# Solution: Clean and rebuild
dotnet clean
dotnet build --configuration Release
```

## 🔧 Advanced Configuration

### Custom Build Properties

```bash
# Force package generation in any configuration
dotnet build -p:ForcePackageGeneration=true

# Clean version file during clean
dotnet clean -p:CleanVersionFile=true

# Override package output path
dotnet build --configuration Release -p:PackageOutputPath=/custom/path
```

### CI/CD Integration

Releases are handled entirely by `.github/workflows/release.yml`, triggered by
pushing a `vX.Y.Z` tag that matches the committed `VERSION` file — there is no
CI step that stamps a version. See [docs/release-process.md](release-process.md).

## 📋 Best Practices

### Development

- Use **Debug** configuration for daily development
- Use **Benchmark** configuration for performance testing
- Keep VERSION file in source control
- Test with **Release** configuration before publishing

### Release Management

- Always create explicit version before package builds
- Use semantic versioning (1.2.3, 1.2.3-alpha)
- Test Release configuration before publishing
- Tag releases in git: `git tag v1.2.3`

### Performance

- **Debug**: Fastest for development
- **Benchmark**: Fast + optimized for testing
- **Release**: Moderate (generates packages)

## 🆘 Troubleshooting

### Build Issues

1. Check if VERSION exists for Release/package-validation builds.
2. Clean and rebuild: `dotnet clean && dotnet build`.
3. For package-reference failures, run `dotnet msbuild eng/PackageValidation.proj -target:Clean`, then rerun `-target:Validate`.

### Package Issues

1. Remove repository package-validation state: `dotnet msbuild eng/PackageValidation.proj -target:Clean`.
2. Rerun the complete package gate: `dotnet msbuild eng/PackageValidation.proj -target:Validate`.
3. Inspect `artifacts/package-validation/`; do not clear user-level NuGet caches.

### Version Issues

1. Check current version: `dotnet msbuild -target:ShowVersion`
2. Recreate version: `./scripts/create-release.sh -v X.Y.Z`
3. Verify VERSION file format (single line, no extra whitespace)
