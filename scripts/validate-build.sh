#!/bin/bash

# CVOYA graph Build Validation Script
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
RUN_CODEQL=false

usage() {
    cat <<'EOF'
CVOYA graph Build System Validation

Usage: ./scripts/validate-build.sh [options]

Options:
  --codeql      Run local CodeQL C# analysis after build validation
  -h, --help    Show this help message
EOF
}

while [[ $# -gt 0 ]]; do
    case $1 in
        --codeql)
            RUN_CODEQL=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

print_header "🔍 CVOYA graph Build System Validation"
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
    dotnet clean cvoya-graph.sln --configuration "$config" --verbosity quiet || true
    
    # Restore dependencies
    print_status "Restoring dependencies..."
    dotnet restore cvoya-graph.sln --verbosity quiet -p:Configuration="$config"
    
    # Build configuration
    print_status "Building $config configuration..."
    if dotnet build cvoya-graph.sln --configuration "$config" --no-restore --verbosity minimal; then
        print_status "✅ $config build successful"
    else
        print_error "❌ $config build failed"
        exit 1
    fi
    
    echo ""
done

# Test the repository-scoped package-reference workflow. Any nested restore,
# package inventory, or build failure is a required validation failure.
print_header "Testing package-reference workflow..."
if dotnet msbuild eng/PackageValidation.proj -target:Validate -verbosity:minimal; then
    print_status "✅ Package-reference validation successful"
else
    print_error "❌ Package-reference validation failed"
    exit 1
fi

echo ""

# Test MSBuild targets
print_header "Testing MSBuild targets..."

# Test ShowVersion target
print_status "Testing ShowVersion target..."
dotnet msbuild -target:ShowVersion -verbosity:quiet

# Check for common issues
print_header "Checking for common issues..."

# Check for missing README files
for project in src/*/; do
    for project_file in "$project"*.csproj; do
        [ -e "$project_file" ] || continue
        if [ ! -f "${project}README.md" ]; then
            print_warning "⚠️  Missing README.md in $(basename "$project")"
        fi
        break
    done
done

# Check for proper project structure
if [ ! -f "cvoya-graph.sln" ]; then
    print_error "❌ Solution file not found"
    exit 1
fi

print_status "✅ Solution file found"

echo ""

if [ "$RUN_CODEQL" = true ]; then
    print_header "Running CodeQL analysis..."
    if ./scripts/run-codeql.sh; then
        print_status "✅ CodeQL analysis completed"
    else
        print_error "❌ CodeQL analysis failed"
        exit 1
    fi

    echo ""
fi

print_header "🎉 Build system validation completed successfully!"
echo ""
print_status "All configurations are working correctly."
print_status "The build system is ready for development and CI/CD." 
