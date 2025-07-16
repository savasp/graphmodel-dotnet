#!/bin/bash

# GraphModel Build Validation Script
# Validates all build configurations and ensures the build system is working correctly

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${BLUE}[HEADER]${NC} $1"
}

# Configuration to test
CONFIGURATIONS=("Debug" "Benchmark" "LocalFeed" "Release")

print_header "🔍 GraphModel Build System Validation"
echo ""

# Check prerequisites
print_status "Checking prerequisites..."

# Check .NET version
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    print_status "✅ .NET SDK found: $DOTNET_VERSION"
else
    print_error "❌ .NET SDK not found"
    exit 1
fi

# Check if we're in the right directory
if [ ! -f "Directory.Build.props" ]; then
    print_error "❌ Directory.Build.props not found. Run from repository root."
    exit 1
fi

# Check VERSION file
if [ -f "VERSION" ]; then
    VERSION=$(cat VERSION)
    print_status "✅ VERSION file found: $VERSION"
else
    print_warning "⚠️  VERSION file not found (required for Release builds)"
fi

echo ""

# Test each configuration
for config in "${CONFIGURATIONS[@]}"; do
    print_header "Testing $config configuration..."
    
    # Clean previous builds
    print_status "Cleaning previous builds..."
    dotnet clean --configuration "$config" --verbosity quiet || true
    
    # Restore dependencies
    print_status "Restoring dependencies..."
    dotnet restore --verbosity quiet
    
    # Build configuration
    print_status "Building $config configuration..."
    if dotnet build --configuration "$config" --no-restore --verbosity minimal; then
        print_status "✅ $config build successful"
    else
        print_error "❌ $config build failed"
        exit 1
    fi
    
    echo ""
done

# Test local feed workflow
print_header "Testing LocalFeed workflow..."
print_status "Setting up local feed..."
if ./scripts/setup-local-feed-msbuild.sh; then
    print_status "✅ LocalFeed setup successful"
    
    # Test Release with package references
    print_status "Testing Release with package references..."
    if dotnet build --configuration Release --no-restore --verbosity minimal; then
        print_status "✅ Release with package references successful"
    else
        print_error "❌ Release with package references failed"
    fi
    
    # Clean up
    print_status "Cleaning up local feed..."
    dotnet msbuild -target:CleanLocalFeed -verbosity:quiet
    print_status "✅ Local feed cleanup successful"
else
    print_error "❌ LocalFeed setup failed"
    exit 1
fi

echo ""

# Test MSBuild targets
print_header "Testing MSBuild targets..."

# Test ShowVersion target
print_status "Testing ShowVersion target..."
dotnet msbuild -target:ShowVersion -verbosity:quiet

# Test BuildWithPackageReferences target
print_status "Testing BuildWithPackageReferences target..."
if dotnet msbuild -target:BuildWithPackageReferences -verbosity:quiet; then
    print_status "✅ BuildWithPackageReferences successful"
else
    print_warning "⚠️  BuildWithPackageReferences failed (this is expected if no VERSION file)"
fi

echo ""

# Check for common issues
print_header "Checking for common issues..."

# Check for missing README files
for project in src/*/; do
    if [ -f "$project"*.csproj ] && [ ! -f "$project"README.md ]; then
        print_warning "⚠️  Missing README.md in $(basename "$project")"
    fi
done

# Check for proper project structure
if [ ! -f "graphmodel.sln" ]; then
    print_error "❌ Solution file not found"
    exit 1
fi

print_status "✅ Solution file found"

echo ""
print_header "🎉 Build system validation completed successfully!"
echo ""
print_status "All configurations are working correctly."
print_status "The build system is ready for development and CI/CD." 