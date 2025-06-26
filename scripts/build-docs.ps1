# GraphModel Documentation Builder
# Builds all source projects and copies XML documentation to docs/api folder

param(
    [string]$Configuration = "Release",
    [switch]$Help
)

if ($Help) {
    Write-Host "GraphModel Documentation Builder"
    Write-Host ""
    Write-Host "Usage: ./scripts/build-docs.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Configuration <config>  Build configuration (default: Release)"
    Write-Host "  -Help                    Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  ./scripts/build-docs.ps1                    # Build with Release configuration"
    Write-Host "  ./scripts/build-docs.ps1 -Configuration Debug  # Build with Debug configuration"
    exit 0
}

Write-Host "🔨 Building GraphModel documentation..." -ForegroundColor Green
Write-Host "📋 Using configuration: $Configuration" -ForegroundColor Cyan

# Build all source projects to generate XML documentation
Write-Host "🏗️  Building source projects..." -ForegroundColor Green

$sourceProjects = Get-ChildItem -Path "src" -Directory
foreach ($project in $sourceProjects) {
    $projectFiles = Get-ChildItem -Path $project.FullName -Filter "*.csproj"
    if ($projectFiles.Count -gt 0) {
        Write-Host "  📦 Building $($project.Name)..." -ForegroundColor Yellow
        dotnet build $project.FullName --configuration $Configuration --no-restore --verbosity quiet
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to build $($project.Name)"
            exit $LASTEXITCODE
        }
    }
}

# Verify XML files were copied
Write-Host ""
Write-Host "📚 Generated XML documentation files:" -ForegroundColor Green

if (Test-Path "docs/api") {
    $xmlFiles = Get-ChildItem -Path "docs/api" -Filter "*.xml"
    foreach ($file in $xmlFiles) {
        $sizeKB = [math]::Round($file.Length / 1KB, 1)
        Write-Host "  ✅ $($file.Name) ($sizeKB KB)" -ForegroundColor Green
    }
    
    if ($xmlFiles.Count -eq 0) {
        Write-Warning "No XML files found in docs/api directory"
    }
} else {
    Write-Error "docs/api directory not found"
    exit 1
}

Write-Host ""
Write-Host "✅ Documentation build completed!" -ForegroundColor Green
Write-Host "📂 XML files available in: docs/api/" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Usage with documentation generators:" -ForegroundColor Yellow
Write-Host "   - DocFX: Use these XML files as input"
Write-Host "   - Sandcastle: Reference the XML files"
Write-Host "   - Custom tools: Parse XML for API documentation" 