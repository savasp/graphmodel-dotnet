# GraphModel Scripts

This directory contains utility scripts for the GraphModel project.

## 🚀 Quick Start Commands

### Version Management

```bash
# Create a new release version
dotnet msbuild -target:CreateRelease -p:ReleaseVersion=1.2.3

# Show current version
dotnet msbuild -target:ShowVersion
```

### Build Commands

```bash
# Development build (project references)
dotnet build --configuration Debug

# Performance testing build (project references + optimizations)
dotnet build --configuration Benchmark

# Production build (package references)
dotnet build --configuration Release
```

### Package Management

```bash
# Clean build outputs
dotnet clean

# Run benchmarks
./scripts/run-benchmarks.sh
```

## 📖 Complete Documentation

For comprehensive build system documentation, see: **[docs/BUILD_SYSTEM.md](../docs/BUILD_SYSTEM.md)**

## 📦 Build Configurations

| Configuration | Project Refs | Optimizations | Packages | VERSION Required | Use Case              |
| ------------- | ------------ | ------------- | -------- | ---------------- | --------------------- |
| **Debug**     | ✅ Yes       | ❌ No         | ❌ No    | ❌ No            | Development           |
| **Benchmark** | ✅ Yes       | ✅ Yes        | ❌ No    | ❌ No            | Performance testing   |
| **LocalFeed** | ✅ Yes       | ✅ Yes        | ✅ Yes   | ❌ No            | Local package testing |
| **Release**   | ❌ No        | ✅ Yes        | ✅ Yes   | ✅ Yes           | Production builds     |

## Local NuGet Feed Scripts

For testing Release configuration with local packages before publishing:

### `setup-local-feed-msbuild.sh` ⭐ **Recommended**

Uses MSBuild integration to automatically create a local NuGet feed with all GraphModel packages:

```bash
# Set up local feed using script
./scripts/setup-local-feed-msbuild.sh

# Or build directly with LocalFeed configuration
dotnet build --configuration LocalFeed

# Test Release configuration
dotnet build --configuration Release
```

**What it does:**

- Uses the **LocalFeed** configuration (defined in `Directory.Build.props`)
- Builds with Release optimizations but using project references
- Automatically generates packages and sets up local NuGet feed
- MSBuild handles dependency resolution and build ordering
- Enables testing Release configuration with local packages

**Advantages:**

- ✅ Integrated with MSBuild - no manual dependency management
- ✅ Uses existing build infrastructure
- ✅ Automatic package generation and feed setup
- ✅ Proper dependency resolution
- ✅ Clean separation of concerns

**How it works:**

1. **LocalFeed Configuration**: A new build configuration that combines:

   - Release-level optimizations (`<Optimize>true</Optimize>`)
   - Project references for fast builds (`UseProjectReferences=true`)
   - Automatic package generation (`GeneratePackageOnBuild=true`)

2. **MSBuild Targets**: Automatic local feed management:

   - `SetupLocalFeed`: Creates local feed directory and NuGet source (runs before LocalFeed builds)
   - `PublishToLocalFeed`: Copies packages to local feed after packaging
   - `CleanLocalFeed`: Removes local feed and cleans up
   - `TestLocalFeed`: Complete end-to-end testing workflow

3. **Smart Package Versioning**: Uses automatic versioning with timestamp suffix

4. **Sentinel File System**: Prevents duplicate NuGet source registration

**Cleanup:**

```bash
dotnet msbuild -target:CleanLocalFeed
```

**Testing Results:**

✅ All 5 packages created successfully  
✅ Local feed setup works automatically  
✅ Release configuration builds with package references  
✅ MSBuild integration prevents conflicts  
✅ LocalFeed configuration implemented and working  
✅ Automatic package publishing to local feed

### `setup-local-feed-simple.sh` / `setup-local-feed.ps1` (Legacy)

Manual approach for creating local NuGet feed (use MSBuild approach above instead):

```bash
# Build packages and set up local feed
./scripts/setup-local-feed-simple.sh

# Test Release configuration
dotnet restore --force
dotnet build --configuration Release -p:Version=1.0.0-local.YYYYMMDDHHMMSS
```

**What it does:**

- Builds all source projects with project references (Benchmark configuration)
- Creates NuGet packages from built outputs
- Sets up a local NuGet feed in `./local-nuget-feed/`
- Configures NuGet to use the local feed
- Outputs the version to use for testing

### `cleanup-local-feed.sh`

Removes the local NuGet feed and cleans up:

```bash
# Clean up local feed
./scripts/cleanup-local-feed.sh
```

**What it does:**

- Removes the local NuGet source
- Deletes `./local-nuget-feed/` and `./artifacts/` directories
- Clears NuGet cache
- Restores normal project reference behavior

### PowerShell Equivalents

- `setup-local-feed.ps1` - PowerShell version of the setup script
- Both scripts work cross-platform

## 📚 Documentation Build Scripts

### `build-docs.sh` (Bash)

A Bash script for building XML documentation from all source projects.

**Usage:**

```bash
./scripts/build-docs.sh [configuration]
```

**Parameters:**

- `configuration`: Build configuration (default: `Release`)

**Examples:**

```bash
# Build documentation with Release configuration
./scripts/build-docs.sh

# Build documentation with Debug configuration
./scripts/build-docs.sh Debug

# Build documentation with Benchmark configuration
./scripts/build-docs.sh Benchmark
```

### `build-docs.ps1` (PowerShell)

A PowerShell script for building XML documentation on Windows or PowerShell Core.

**Usage:**

```powershell
./scripts/build-docs.ps1 [options]
```

**Parameters:**

- `-Configuration <config>`: Build configuration (default: `Release`)
- `-Help`: Show help message

**Examples:**

```powershell
# Build documentation with Release configuration
./scripts/build-docs.ps1

# Build documentation with Debug configuration
./scripts/build-docs.ps1 -Configuration Debug

# Show help
./scripts/build-docs.ps1 -Help
```

## 🏃‍♂️ Performance Benchmark Scripts

### `run-benchmarks.ps1` (PowerShell)

A PowerShell script for running performance benchmarks on Windows or PowerShell Core.

**Usage:**

```powershell
./scripts/run-benchmarks.ps1 [options]
```

**Parameters:**

- `-Mode <mode>`: Benchmark mode (default: `all`)
  - `all`: Run all benchmarks automatically
  - `crud`: Run only CRUD operation benchmarks
  - `relationships`: Run only relationship benchmarks
  - `interactive`: Interactive benchmark selection
- `-OutputDir <dir>`: Output directory for results (default: `./benchmarks`)
- `-Help`: Show help message

**Examples:**

```powershell
# Run all benchmarks
./scripts/run-benchmarks.ps1

# Run only CRUD benchmarks
./scripts/run-benchmarks.ps1 -Mode crud

# Interactive selection with custom output
./scripts/run-benchmarks.ps1 -Mode interactive -OutputDir "./my-results"
```

### `run-benchmarks.sh` (Bash)

A Bash script for running performance benchmarks on macOS/Linux.

**Usage:**

```bash
./scripts/run-benchmarks.sh [options]
```

**Options:**

- `-m, --mode <mode>`: Benchmark mode (default: `all`)
  - `all`: Run all benchmarks automatically
  - `crud`: Run only CRUD operation benchmarks
  - `relationships`: Run only relationship benchmarks
  - `interactive`: Interactive benchmark selection
- `-o, --output <dir>`: Output directory for results (default: `./benchmarks`)
- `-h, --help`: Show help message

**Examples:**

```

```
