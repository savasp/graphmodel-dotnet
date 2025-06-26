# GraphModel Build System Guide

This document explains the GraphModel build configurations, version management, and development workflows.

## 🚀 Quick Start

### Essential Commands

```bash
# Development build (fastest, project references)
dotnet build --configuration Debug

# Create a release version
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3

# Build production packages
dotnet build --configuration Release

# Run performance benchmarks
./scripts/run-benchmarks.sh
```

## 📦 Build Configurations

GraphModel uses **three distinct build configurations** optimized for different scenarios:

| Configuration | Project Refs | Optimizations | Packages | VERSION Required | Use Case                |
| ------------- | ------------ | ------------- | -------- | ---------------- | ----------------------- |
| **Debug**     | ✅ Yes       | ❌ No         | ❌ No    | ❌ No            | Development & debugging |
| **Benchmark** | ✅ Yes       | ✅ Yes        | ❌ No    | ❌ No            | Performance testing     |
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

#### Release Configuration

- **Purpose**: Production builds and publishing
- **Speed**: Fully optimized
- **Dependencies**: NuGet package references only
- **Packages**: Generated for publishing
- **Usage**: `dotnet build --configuration Release`

## 🏷️ Version Management

GraphModel uses a **VERSION file-based system** that ensures consistent versioning across all packages and prevents accidental releases.

### VERSION File Format

The `VERSION` file contains a single line with the version:

```
# Stable release
1.2.3

# Pre-release
1.2.3-alpha
1.2.3-beta.1
1.2.3-rc.2
```

### Creating Releases

#### Method 1: MSBuild Target (Recommended)

```bash
# Create stable release
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3

# Create pre-release
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3-alpha

# Create timestamped pre-release
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3-alpha-20250125120000
```

#### Method 2: Helper Scripts

```bash
# Interactive version creation
./scripts/create-release.sh

# Non-interactive with options
./scripts/create-release.sh -v 1.2.3 --build-local --commit

# PowerShell version
./scripts/create-release.ps1 -Version 1.2.3 -BuildLocal -Commit
```

### Version Requirements

- **Debug/Benchmark**: No VERSION file needed (uses default version with timestamp)
- **Release**: VERSION file required for consistent package versions

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

### Release Preparation Workflow

```bash
# 1. Create release version
./scripts/create-release.sh -v 1.2.3

# 2. Verify release build works
dotnet build --configuration Release

# 3. Run full test suite
dotnet test --configuration Release

# 4. Commit and tag
git add VERSION
git commit -m "Release 1.2.3"
git tag v1.2.3
```

### Package Testing Workflow

```bash
# 1. Create test version
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3-test

# 2. Build and test packages
dotnet build --configuration Release

# 3. Test in examples/tests
dotnet test --configuration Release
```

## 🛠️ Available MSBuild Targets

### Version Management

```bash
# Create new release version
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=X.Y.Z

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

### Cleanup Commands

```bash
# Clean build outputs
dotnet clean

# Clear NuGet cache
dotnet nuget locals all --clear
```

## 📁 Directory Structure

```
graphmodel/
├── VERSION                    # Current release version
├── Directory.Build.props      # MSBuild configuration
├── artifacts/                # Built packages
└── scripts/
    ├── create-release.sh     # Release creation helper
    ├── create-release.ps1    # PowerShell version
    ├── run-benchmarks.sh     # Benchmark runner
    └── cleanup-local-feed.sh # Legacy cleanup script
```

## 🚨 Error Prevention

### Common Errors and Solutions

#### "VERSION file is required for package generation"

```bash
# Solution: Create a release version first
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3
```

#### "Package reference could not be resolved"

```bash
# Solution: Ensure packages are built and available
dotnet build --configuration Release
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

```yaml
# GitHub Actions example
- name: Create Release
  run: dotnet msbuild -target:CreateRelease -p:ReleaseVersion=${{ github.ref }}

- name: Build Packages
  run: dotnet build --configuration Release

- name: Publish Packages
  run: dotnet nuget push artifacts/*.nupkg
```

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

1. Check current version: `dotnet msbuild -target:ShowVersion`
2. Recreate version: `dotnet msbuild -target:CreateRelease -p:ReleaseVersion=X.Y.Z`
3. Verify VERSION file format (single line, no extra whitespace)
