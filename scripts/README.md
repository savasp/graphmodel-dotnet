# GraphModel Scripts

This directory contains utility scripts for the GraphModel project.

## 🚀 Quick Start Commands

### Build System Validation

```bash
# Check project status
./scripts/status.sh

# Validate all build configurations
./scripts/validate-build.sh
```

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

# Local package testing (project references + packages)
dotnet build --configuration LocalFeed

# Production build (package references)
dotnet build --configuration Release
```

### Testing

```bash
# Run all tests
./scripts/run-tests.sh

# Run tests with coverage
./scripts/run-tests.sh --coverage

# Run tests with containers
./scripts/run-tests.sh --neo4j --seq

# Run performance tests
./scripts/run-tests.sh --performance
```

### Package Management

```bash
# Set up local NuGet feed for testing
./scripts/setup-local-feed-msbuild.sh

# Clean up local feed
./scripts/cleanup-local-feed.sh

# Run benchmarks
./scripts/run-benchmarks.sh
```

### Cleanup

```bash
# Clean build artifacts
./scripts/clean-all.sh

# Clean everything (containers, cache, artifacts)
./scripts/clean-all.sh --all
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

## 🛠️ Development Tools Scripts

### `status.sh` ⭐ **New**

Shows the current state of the build system, containers, and project.

**Usage:**

```bash
./scripts/status.sh
```

**What it does:**

- Shows project information (version, .NET SDK)
- Reports build artifacts status
- Checks container status (Neo4j, Seq)
- Shows test results status
- Provides recommendations and quick actions

### `validate-build.sh` ⭐ **New**

Validates the entire build system and ensures all configurations work correctly.

**Usage:**

```bash
./scripts/validate-build.sh
```

**What it does:**

- Tests all build configurations (Debug, Benchmark, LocalFeed, Release)
- Validates MSBuild targets and local feed workflow
- Checks prerequisites and project structure
- Ensures the build system is ready for development and CI/CD

### `run-tests.sh` ⭐ **New**

Comprehensive test runner with support for different test types and configurations.

**Usage:**

```bash
./scripts/run-tests.sh [options]
```

**Options:**

- `-c, --configuration <config>`: Build configuration (default: Release)
- `-v, --verbosity <level>`: Test verbosity (default: normal)
- `--coverage`: Collect code coverage
- `--neo4j`: Start Neo4j container before tests
- `--seq`: Start Seq container before tests
- `--no-analyzers`: Skip analyzer tests
- `--no-neo4j`: Skip Neo4j tests
- `--performance`: Run performance tests

**Examples:**

```bash
./scripts/run-tests.sh                                    # Run all tests
./scripts/run-tests.sh --coverage                        # Run with coverage
./scripts/run-tests.sh --neo4j --seq                     # Start containers and run tests
./scripts/run-tests.sh --performance                     # Run performance tests
./scripts/run-tests.sh -c Debug --no-neo4j               # Debug build, skip Neo4j tests
```

### `clean-all.sh` ⭐ **New**

Comprehensive cleanup script that removes all build artifacts, containers, and temporary files.

**Usage:**

```bash
./scripts/clean-all.sh [options]
```

**Options:**

- `--containers`: Clean up containers (Neo4j, Seq)
- `--nuget`: Clean NuGet cache
- `--all`: Clean everything (containers, cache, artifacts)

**Examples:**

```bash
./scripts/clean-all.sh                    # Clean build artifacts only
./scripts/clean-all.sh --containers       # Clean containers and artifacts
./scripts/clean-all.sh --all              # Clean everything
```

### `start-neo4j.sh`

Starts the Neo4j container.

**Usage:**

```bash
./scripts/containers/start-neo4j.sh
```

**What it does:**

- Starts the Neo4j container using podman
- If the image doesn't exist locally, it is downloaded

### `start-seq.sh`

Starts Seq log aggregation system for development and testing using Podman.

**Usage:**

```bash
./scripts/containers/start-seq.sh
```

**What it does:**

- Starts Seq container with log aggregation using Podman
- Provides log UI at http://localhost:5341
- Stores log data in `~/tmp/logdata`
- Automatically removes existing containers
