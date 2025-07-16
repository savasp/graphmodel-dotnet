#!/bin/bash

# GraphModel Status Checker
# Shows the current state of the build system, containers, and project

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

print_header "📊 GraphModel Project Status"
echo ""

# Check if we're in the right directory
if [ ! -f "Directory.Build.props" ]; then
    print_error "❌ Directory.Build.props not found. Run from repository root."
    exit 1
fi

# Project Information
print_header "📋 Project Information"

# Check .NET version
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    print_status "✅ .NET SDK: $DOTNET_VERSION"
else
    print_error "❌ .NET SDK not found"
fi

# Check VERSION file
if [ -f "VERSION" ]; then
    VERSION=$(cat VERSION)
    print_status "✅ Version: $VERSION"
else
    print_warning "⚠️  VERSION file not found"
fi

# Check solution file
if [ -f "graphmodel.sln" ]; then
    print_status "✅ Solution file found"
else
    print_error "❌ Solution file not found"
fi

echo ""

# Build Artifacts Status
print_header "🏗️  Build Artifacts"

# Check artifacts directory
if [ -d "artifacts" ]; then
    ARTIFACT_COUNT=$(find artifacts -name "*.nupkg" 2>/dev/null | wc -l)
    print_status "✅ Artifacts directory exists ($ARTIFACT_COUNT packages)"
else
    print_warning "⚠️  No artifacts directory"
fi

# Check local feed
if [ -d "local-nuget-feed" ]; then
    FEED_COUNT=$(find local-nuget-feed -name "*.nupkg" 2>/dev/null | wc -l)
    print_status "✅ Local feed exists ($FEED_COUNT packages)"
else
    print_warning "⚠️  No local feed directory"
fi

# Check build directories
BUILD_DIRS=$(find . -name "bin" -o -name "obj" 2>/dev/null | wc -l)
if [ "$BUILD_DIRS" -gt 0 ]; then
    print_status "✅ Build directories found ($BUILD_DIRS total)"
else
    print_warning "⚠️  No build directories found"
fi

echo ""

# Container Status
print_header "🐳 Container Status"

# Check if podman is available
if command -v podman &> /dev/null; then
    print_status "✅ Podman available"
    
    # Check Neo4j container
    if podman ps --format "table {{.Names}}" | grep -q "neo4j"; then
        print_status "✅ Neo4j container running"
    elif podman ps -a --format "table {{.Names}}" | grep -q "neo4j"; then
        print_warning "⚠️  Neo4j container exists but not running"
    else
        print_warning "⚠️  No Neo4j container found"
    fi
    
    # Check Neo4j pod
    if podman pod exists neo4j-pod; then
        print_status "✅ Neo4j pod exists"
    else
        print_warning "⚠️  No Neo4j pod found"
    fi
    
    # Check Seq container
    if podman ps --format "table {{.Names}}" | grep -q "seq"; then
        print_status "✅ Seq container running"
    elif podman ps -a --format "table {{.Names}}" | grep -q "seq"; then
        print_warning "⚠️  Seq container exists but not running"
    else
        print_warning "⚠️  No Seq container found"
    fi
    
    # Check Neo4j volumes
    if podman volume exists neo4j-data; then
        print_status "✅ Neo4j data volume exists"
    else
        print_warning "⚠️  No Neo4j data volume"
    fi
    
    if podman volume exists neo4j-logs; then
        print_status "✅ Neo4j logs volume exists"
    else
        print_warning "⚠️  No Neo4j logs volume"
    fi
else
    print_warning "⚠️  Podman not available"
fi

echo ""

# Test Results Status
print_header "🧪 Test Results"

# Check coverage directory
if [ -d "coverage" ]; then
    COVERAGE_FILES=$(find coverage -name "*.xml" -o -name "*.html" 2>/dev/null | wc -l)
    print_status "✅ Coverage results exist ($COVERAGE_FILES files)"
else
    print_warning "⚠️  No coverage results"
fi

# Check benchmarks directory
if [ -d "benchmarks" ]; then
    BENCHMARK_FILES=$(find benchmarks -name "*.html" -o -name "*.json" -o -name "*.md" 2>/dev/null | wc -l)
    print_status "✅ Benchmark results exist ($BENCHMARK_FILES files)"
else
    print_warning "⚠️  No benchmark results"
fi

echo ""

# Quick Build Test
print_header "🔨 Quick Build Test"

# Test Debug build
print_status "Testing Debug build..."
if dotnet build --configuration Debug --no-restore --verbosity quiet 2>/dev/null; then
    print_status "✅ Debug build works"
else
    print_error "❌ Debug build failed"
fi

# Test Release build (if VERSION exists)
if [ -f "VERSION" ]; then
    print_status "Testing Release build..."
    if dotnet build --configuration Release --no-restore --verbosity quiet 2>/dev/null; then
        print_status "✅ Release build works"
    else
        print_warning "⚠️  Release build failed (may need local feed setup)"
    fi
else
    print_warning "⚠️  Skipping Release build test (no VERSION file)"
fi

echo ""

# Recommendations
print_header "💡 Recommendations"

if [ ! -f "VERSION" ]; then
    print_warning "• Create a VERSION file for Release builds"
fi

if [ ! -d "local-nuget-feed" ]; then
    print_warning "• Set up local feed for Release testing: ./scripts/setup-local-feed-msbuild.sh"
fi

if ! podman ps --format "table {{.Names}}" | grep -q "neo4j"; then
    print_warning "• Start Neo4j for testing: ./scripts/containers/start-neo4j.sh"
fi

if [ ! -d "coverage" ] && [ ! -d "benchmarks" ]; then
    print_warning "• Run tests: ./scripts/run-tests.sh"
    print_warning "• Run benchmarks: ./scripts/run-benchmarks.sh"
fi

echo ""
print_header "🎯 Quick Actions"

echo "  Validate build system: ./scripts/validate-build.sh"
echo "  Run all tests:        ./scripts/run-tests.sh"
echo "  Clean everything:     ./scripts/clean-all.sh --all"
echo "  Start containers:     ./scripts/containers/start-neo4j.sh"
echo "  Create release:       ./scripts/create-release.sh -v 1.2.3" 