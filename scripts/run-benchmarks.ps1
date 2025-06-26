# GraphModel Performance Benchmarks Runner
# Usage: ./scripts/run-benchmarks.ps1 [options]

param(
    [string]$Mode = "all",           # all, crud, relationships, or interactive
    [string]$OutputDir = "./benchmarks",
    [switch]$Help
)

if ($Help) {
    Write-Host "GraphModel Performance Benchmarks Runner"
    Write-Host ""
    Write-Host "Usage: ./scripts/run-benchmarks.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Mode <mode>      Benchmark mode: all, crud, relationships, or interactive (default: all)"
    Write-Host "  -OutputDir <dir>  Output directory for results (default: ./benchmarks)"
    Write-Host "  -Help             Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  ./scripts/run-benchmarks.ps1                    # Run all benchmarks"
    Write-Host "  ./scripts/run-benchmarks.ps1 -Mode crud         # Run only CRUD benchmarks"
    Write-Host "  ./scripts/run-benchmarks.ps1 -Mode interactive  # Interactive selection"
    exit 0
}

# Ensure we're in the right directory
$projectDir = "tests/Graph.Model.Performance.Tests"
if (-not (Test-Path $projectDir)) {
    Write-Error "Performance test project not found. Make sure you're running from the repository root."
    exit 1
}

# Build the project first
Write-Host "Building performance tests..." -ForegroundColor Green
dotnet build --configuration Benchmark

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit $LASTEXITCODE
}

# Prepare arguments based on mode
$args = @()
switch ($Mode.ToLower()) {
    "all" { 
        $args += "--all"
        Write-Host "Running all benchmarks..." -ForegroundColor Green
    }
    "crud" { 
        $args += "--filter", "*CrudOperations*"
        Write-Host "Running CRUD operation benchmarks..." -ForegroundColor Green
    }
    "relationships" { 
        $args += "--filter", "*Relationship*"
        Write-Host "Running relationship benchmarks..." -ForegroundColor Green
    }
    "interactive" { 
        Write-Host "Starting interactive benchmark selection..." -ForegroundColor Green
    }
    default {
        Write-Error "Invalid mode: $Mode. Use 'all', 'crud', 'relationships', or 'interactive'"
        exit 1
    }
}

# Add common arguments
$args += "--artifacts", $OutputDir
$args += "--exporters", "html", "json", "markdown"

# Run the benchmarks
Write-Host "Running benchmarks with arguments: $($args -join ' ')" -ForegroundColor Yellow
dotnet run --project $projectDir --configuration Benchmark --no-build -- $args

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Benchmarks completed successfully!" -ForegroundColor Green
    Write-Host "Results available in: $OutputDir" -ForegroundColor Cyan
} else {
    Write-Error "Benchmarks failed with exit code $LASTEXITCODE"
} 