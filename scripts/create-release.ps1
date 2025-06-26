[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Version,
    
    [switch]$BuildLocal,
    [switch]$BuildRelease,
    [switch]$Commit,
    [switch]$Help
)

# Show help
if ($Help) {
    Write-Host "GraphModel Release Creator" -ForegroundColor Blue
    Write-Host ""
    Write-Host "Usage: .\create-release.ps1 [OPTIONS]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Version VERSION     Specify version (e.g., 1.2.3 or 1.2.3-alpha)" -ForegroundColor White
    Write-Host "  -BuildLocal          Build Release configuration after creating version" -ForegroundColor White
    Write-Host "  -BuildRelease        Build Release configuration after creating version" -ForegroundColor White
    Write-Host "  -Commit              Commit VERSION file to git" -ForegroundColor White
    Write-Host "  -Help                Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\create-release.ps1 -Version 1.2.3                          # Create stable release" -ForegroundColor Gray
    Write-Host "  .\create-release.ps1 -Version 1.2.3-alpha                    # Create pre-release" -ForegroundColor Gray
    Write-Host "  .\create-release.ps1 -Version 1.2.3 -BuildLocal              # Create and build release" -ForegroundColor Gray
    Write-Host "  .\create-release.ps1 -Version 1.2.3 -BuildRelease -Commit    # Create, build, and commit" -ForegroundColor Gray
    exit 0
}

Write-Host "🚀 GraphModel Release Creator" -ForegroundColor Blue
Write-Host ""

# Prompt for version if not provided
if (-not $Version) {
    $Version = Read-Host "📝 Enter release version (e.g., 1.2.3 or 1.2.3-alpha)"
}

# Validate version format
if (-not $Version) {
    Write-Host "❌ Version cannot be empty" -ForegroundColor Red
    exit 1
}

# Create the release version
Write-Host "🎯 Creating release version: $Version" -ForegroundColor Blue
$result = & dotnet msbuild -target:CreateRelease -p:ReleaseVersion="$Version"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create release version" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Build Release if requested
if ($BuildLocal) {
    Write-Host "🔨 Building Release configuration..." -ForegroundColor Blue
    & dotnet build --configuration Release
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Release build complete" -ForegroundColor Green
    } else {
        Write-Host "❌ Release build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Build Release if requested
if ($BuildRelease) {
    Write-Host "🔨 Building Release configuration..." -ForegroundColor Blue
    & dotnet build --configuration Release
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Release build complete" -ForegroundColor Green
    } else {
        Write-Host "❌ Release build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Commit VERSION file if requested
if ($Commit) {
    if ((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path .git)) {
        Write-Host "📝 Committing VERSION file..." -ForegroundColor Blue
        & git add VERSION
        & git commit -m "Release $Version"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ VERSION file committed" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Git commit failed or no changes to commit" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  Git not available or not in a git repository" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "🎉 Release $Version created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next steps:" -ForegroundColor Blue
if (-not $BuildLocal -and -not $BuildRelease) {
    Write-Host "   • Build release:    dotnet build --configuration Release" -ForegroundColor White
}
if (-not $Commit) {
    Write-Host "   • Commit version:   git add VERSION && git commit -m 'Release $Version'" -ForegroundColor White
}
Write-Host "   • Test packages:    dotnet build --configuration Release" -ForegroundColor White
Write-Host "   • Publish packages: dotnet nuget push artifacts/*.nupkg" -ForegroundColor White 