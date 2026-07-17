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
dotnet build --configuration LocalFeed

# Cut a release (--plan previews the tag without pushing)
./scripts/release.sh 1.2.3

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
| **Release**   | ❌ No        | ✅ Yes        | ✅ Yes   | ✅ Yes           | Production builds       |

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

- **Purpose**: Testing package references locally before publishing
- **Speed**: Optimized builds with fast project references
- **Dependencies**: Direct project references (fast builds)
- **Packages**: Generated and published to local NuGet feed
- **Usage**: `dotnet build --configuration LocalFeed`

#### Release Configuration

- **Purpose**: Production builds and publishing
- **Speed**: Fully optimized
- **Dependencies**: NuGet package references only
- **Packages**: Generated for publishing
- **Usage**: `dotnet build --configuration Release`

## 🏷️ Version Management

CVOYA graph releases are **tag-authoritative**: the pushed tag *is* the version.
Nothing in CI writes or stamps a version, which is what keeps package versions
consistent and prevents accidental releases.

### Version Scheme

```text
MAJOR.MINOR.PATCH[-(alpha|beta|rc).YYYYMMDD[.N]]
```

```text
# Stable release
1.2.3

# Date-anchored pre-releases (the .1 is a same-day counter)
1.2.3-alpha.20260716
1.2.3-rc.20260716.1
```

### Creating Releases

`scripts/release.sh` computes the version, pushes the tag, and `release.yml` reads
it back — nothing writes a version to a file, so nothing can be stamped twice.

```bash
# Preview the computed tag without pushing anything
./scripts/release.sh 1.2.3 --pre alpha --plan

# Cut a stable release  -> v1.2.3
./scripts/release.sh 1.2.3

# Cut a date-anchored pre-release  -> v1.2.3-alpha.20260716
./scripts/release.sh 1.2.3 --pre alpha

# ...and make it the current Latest on GitHub
./scripts/release.sh 1.2.3 --pre alpha --latest
```

The base version is positional and pre-release suffixes come from `--pre` — they
are never typed by hand. See `./scripts/release.sh --help` for the full option
list, and [release-process.md](release-process.md) for the full process.

### The VERSION file

`VERSION` is the **development default** for untagged local and CI builds, not the
release source of truth. Editing it does not cut a release, and cutting a release
does not edit it. `release.yml` passes the tag's version to the pack job as
`GRAPH_RELEASE_VERSION`, which `Directory.Build.props` prefers over the file.

### Publishing a Release

Publishing is entirely tag-triggered — there is no manual publish step:

1. Run `./scripts/release.sh X.Y.Z [--pre alpha|beta|rc]` from a checkout whose
   HEAD is on `origin/main`. It computes the version, runs the pre-flight checks,
   and pushes the `vX.Y.Z` tag.
2. `.github/workflows/release.yml` reads the version off the tag and rejects it
   if it doesn't match the version scheme, runs the full test suite including the
   Neo4j and Apache AGE provider tests, packs every package, publishes to NuGet
   using **Trusted Publishing** (OIDC — no stored API key), attests build
   provenance, and creates the GitHub Release with generated notes.
3. `release.sh` watches that run and then verifies every published package
   actually resolves on nuget.org before reporting success.

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
# 1. Verify the release build works
dotnet build --configuration Release

# 2. Run the full test suite
dotnet test --configuration Release

# 3. Preview the tag that would be cut
./scripts/release.sh 1.2.3 --plan

# 4. Cut it — release.sh pushes the tag, watches release.yml, and verifies
#    the published packages resolve on nuget.org
./scripts/release.sh 1.2.3
```

`release.sh` must run from a checkout whose HEAD is on `origin/main`; it refuses
to tag anything else. Steps 1 and 2 are a local smoke test — `release.yml` reruns
the full suite against the tagged commit regardless.

### Package Testing Workflow

For testing package references locally before publishing to NuGet.org:

#### Method 1: Using LocalFeed Configuration (Recommended)

```bash
# 1. Build packages and set up local feed
dotnet build --configuration LocalFeed

# 2. Test Release configuration with package references
dotnet build --configuration Release

# 3. Run tests with package references
dotnet test --configuration Release

# 4. Clean up when done
dotnet msbuild -target:CleanLocalFeed
```

#### Method 2: Using Helper Script

```bash
# 1. Set up local feed using script
./scripts/setup-local-feed-msbuild.sh

# 2. Test Release configuration
dotnet build --configuration Release

# 3. Clean up
dotnet msbuild -target:CleanLocalFeed
```

#### Method 3: Legacy Manual Testing

```bash
# 1. Build and test packages at an arbitrary version, without touching VERSION
GRAPH_RELEASE_VERSION=1.2.3-test dotnet build --configuration Release

# 2. Test in examples/tests
dotnet test --configuration Release
```

## 🛠️ Available MSBuild Targets

### Version Management

```bash
# Cut a release (pushes the tag; --plan to preview)
./scripts/release.sh X.Y.Z [--pre alpha|beta|rc]

# Show the version the current tree would build as
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
# Set up local NuGet feed (builds LocalFeed configuration)
dotnet build --configuration LocalFeed

# Test complete workflow (LocalFeed + Release)
dotnet msbuild -target:TestLocalFeed

# Clean up local feed
dotnet msbuild -target:CleanLocalFeed
```

### Cleanup Commands

```bash
# Clean build outputs
dotnet clean

# Clear NuGet cache
dotnet nuget locals all --clear
```

## 📁 Directory Structure

```text
graphmodel/
├── VERSION                    # Development default version for untagged builds
├── Directory.Build.props      # MSBuild configuration
├── artifacts/                # Built packages
└── scripts/
    ├── release.sh            # Release orchestration (tag, watch, verify)
    ├── run-benchmarks.sh     # Benchmark runner
    └── cleanup-local-feed.sh # Legacy cleanup script
```

## 🚨 Error Prevention

### Common Errors and Solutions

#### "VERSION file is required for package generation"

```bash
# Solution: supply a version for this build without editing VERSION
GRAPH_RELEASE_VERSION=1.2.3 dotnet build --configuration Release
```

#### "Package reference could not be resolved"

```bash
# Solution: Ensure packages are built and available
dotnet build --configuration Release
```

#### `NU5026`, or a package whose contents don't match its version

Always build before packing locally:

```bash
GRAPH_RELEASE_VERSION=1.2.3 dotnet build --configuration Release
GRAPH_RELEASE_VERSION=1.2.3 dotnet pack --configuration Release --no-build -o ./artifacts src/Graph/Graph.csproj
```

`dotnet pack` on its own does **not** build here. `Release` sets
`GeneratePackageOnBuild=true`, and NuGet drops `Build` from the pack graph when
that is on (`NuGet.Build.Tasks.Pack.targets`), so packing either fails with
`NU5026` on a clean tree or — worse — silently packs whatever stale assemblies are
already in `bin/Release` under whatever version you just passed. Releases are
unaffected: `release.yml` always builds first, on a fresh checkout.

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

1. Check if VERSION file exists for Release builds
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Clear NuGet cache: `dotnet nuget locals all --clear`

### Package Issues

1. Clean build outputs: `dotnet clean`
2. Clear NuGet cache: `dotnet nuget locals all --clear`
3. Rebuild packages: `dotnet build --configuration Release`

### Version Issues

1. Check the resolved version: `dotnet msbuild -target:ShowVersion`
2. Override it for one build: `GRAPH_RELEASE_VERSION=X.Y.Z dotnet build -c Release`
3. Verify VERSION file format (single line, no extra whitespace)
