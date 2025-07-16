#!/bin/bash

# GraphModel Comprehensive Cleanup Script
# Removes all build artifacts, containers, and temporary files

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

# Default values
CLEAN_CONTAINERS=false
CLEAN_NUGET_CACHE=false
CLEAN_ALL=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --containers)
            CLEAN_CONTAINERS=true
            shift
            ;;
        --nuget)
            CLEAN_NUGET_CACHE=true
            shift
            ;;
        --all)
            CLEAN_ALL=true
            shift
            ;;
        -h|--help)
            echo "GraphModel Comprehensive Cleanup Script"
            echo ""
            echo "Usage: ./scripts/clean-all.sh [options]"
            echo ""
            echo "Options:"
            echo "  --containers    Clean up containers (Neo4j, Seq)"
            echo "  --nuget         Clean NuGet cache"
            echo "  --all           Clean everything (containers, cache, artifacts)"
            echo "  -h, --help      Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./scripts/clean-all.sh                    # Clean build artifacts only"
            echo "  ./scripts/clean-all.sh --containers       # Clean containers and artifacts"
            echo "  ./scripts/clean-all.sh --all              # Clean everything"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

print_header "🧹 GraphModel Comprehensive Cleanup"
echo ""

# Check if we're in the right directory
if [ ! -f "Directory.Build.props" ]; then
    print_error "❌ Directory.Build.props not found. Run from repository root."
    exit 1
fi

# Clean MSBuild artifacts
print_header "Cleaning MSBuild artifacts..."
print_status "Cleaning local NuGet feed..."
dotnet msbuild -target:CleanLocalFeed -verbosity:quiet || true

print_status "Cleaning solution..."
dotnet clean --verbosity quiet || true

# Clean build directories
print_status "Removing build directories..."
for dir in bin obj; do
    find . -name "$dir" -type d -exec rm -rf {} + 2>/dev/null || true
done

# Clean artifacts directory
if [ -d "artifacts" ]; then
    print_status "Removing artifacts directory..."
    rm -rf artifacts
fi

# Clean coverage directory
if [ -d "coverage" ]; then
    print_status "Removing coverage directory..."
    rm -rf coverage
fi

# Clean benchmarks directory
if [ -d "benchmarks" ]; then
    print_status "Removing benchmarks directory..."
    rm -rf benchmarks
fi

# Clean local NuGet feed directory
if [ -d "local-nuget-feed" ]; then
    print_status "Removing local NuGet feed directory..."
    rm -rf local-nuget-feed
fi

print_status "✅ MSBuild artifacts cleaned"

# Clean containers if requested
if [ "$CLEAN_CONTAINERS" = true ] || [ "$CLEAN_ALL" = true ]; then
    print_header "Cleaning containers..."
    
    # Stop and remove Neo4j container
    if podman ps -a --format "table {{.Names}}" | grep -q "neo4j"; then
        print_status "Stopping Neo4j container..."
        podman stop neo4j 2>/dev/null || true
        podman rm neo4j 2>/dev/null || true
    fi
    
    # Stop and remove Neo4j pod
    if podman pod exists neo4j-pod; then
        print_status "Removing Neo4j pod..."
        podman pod stop neo4j-pod 2>/dev/null || true
        podman pod rm neo4j-pod 2>/dev/null || true
    fi
    
    # Stop and remove Seq container
    if podman ps -a --format "table {{.Names}}" | grep -q "seq"; then
        print_status "Stopping Seq container..."
        podman stop seq 2>/dev/null || true
        podman rm seq 2>/dev/null || true
    fi
    
    # Remove Neo4j volumes
    if podman volume exists neo4j-data; then
        print_status "Removing Neo4j data volume..."
        podman volume rm neo4j-data 2>/dev/null || true
    fi
    
    if podman volume exists neo4j-logs; then
        print_status "Removing Neo4j logs volume..."
        podman volume rm neo4j-logs 2>/dev/null || true
    fi
    
    print_status "✅ Containers cleaned"
fi

# Clean NuGet cache if requested
if [ "$CLEAN_NUGET_CACHE" = true ] || [ "$CLEAN_ALL" = true ]; then
    print_header "Cleaning NuGet cache..."
    
    print_status "Clearing NuGet cache..."
    dotnet nuget locals all --clear || true
    
    print_status "✅ NuGet cache cleaned"
fi

# Clean temporary files
print_header "Cleaning temporary files..."

# Remove .DS_Store files (macOS)
if [ "$(uname)" = "Darwin" ]; then
    print_status "Removing .DS_Store files..."
    find . -name ".DS_Store" -delete 2>/dev/null || true
fi

# Remove temporary files
print_status "Removing temporary files..."
find . -name "*.tmp" -delete 2>/dev/null || true
find . -name "*.temp" -delete 2>/dev/null || true

# Remove log files
print_status "Removing log files..."
find . -name "*.log" -delete 2>/dev/null || true

print_status "✅ Temporary files cleaned"

# Clean log data directory
if [ -d "${HOME}/tmp/logdata" ]; then
    print_status "Removing log data directory..."
    rm -rf "${HOME}/tmp/logdata"
fi

echo ""
print_header "🎉 Cleanup completed successfully!"

if [ "$CLEAN_ALL" = true ]; then
    print_status "All artifacts, containers, and cache have been cleaned."
else
    print_status "Build artifacts have been cleaned."
    if [ "$CLEAN_CONTAINERS" = true ]; then
        print_status "Containers have been cleaned."
    fi
    if [ "$CLEAN_NUGET_CACHE" = true ]; then
        print_status "NuGet cache has been cleaned."
    fi
fi

echo ""
print_status "You can now start fresh with:"
print_status "  dotnet restore"
print_status "  dotnet build" 