#!/bin/bash

# GraphModel Test Runner
# Runs all tests with proper configuration and reporting

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
CONFIGURATION="Release"
VERBOSITY="normal"
COLLECT_COVERAGE=false
START_NEO4J=false
START_SEQ=false
RUN_ANALYZERS=true
RUN_NEO4J=true
RUN_PERFORMANCE=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -v|--verbosity)
            VERBOSITY="$2"
            shift 2
            ;;
        --coverage)
            COLLECT_COVERAGE=true
            shift
            ;;
        --neo4j)
            START_NEO4J=true
            shift
            ;;
        --seq)
            START_SEQ=true
            shift
            ;;
        --no-analyzers)
            RUN_ANALYZERS=false
            shift
            ;;
        --no-neo4j)
            RUN_NEO4J=false
            shift
            ;;
        --performance)
            RUN_PERFORMANCE=true
            shift
            ;;
        -h|--help)
            echo "GraphModel Test Runner"
            echo ""
            echo "Usage: ./scripts/run-tests.sh [options]"
            echo ""
            echo "Options:"
            echo "  -c, --configuration <config>  Build configuration (default: Release)"
            echo "  -v, --verbosity <level>       Test verbosity (default: normal)"
            echo "  --coverage                    Collect code coverage"
            echo "  --neo4j                       Start Neo4j container before tests"
            echo "  --seq                         Start Seq container before tests"
            echo "  --no-analyzers                Skip analyzer tests"
            echo "  --no-neo4j                    Skip Neo4j tests"
            echo "  --performance                 Run performance tests"
            echo "  -h, --help                    Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./scripts/run-tests.sh                                    # Run all tests"
            echo "  ./scripts/run-tests.sh --coverage                        # Run with coverage"
            echo "  ./scripts/run-tests.sh --neo4j --seq                     # Start containers and run tests"
            echo "  ./scripts/run-tests.sh --performance                     # Run performance tests"
            echo "  ./scripts/run-tests.sh -c Debug --no-neo4j               # Debug build, skip Neo4j tests"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

print_header "🧪 GraphModel Test Runner"
echo ""

# Check prerequisites
print_status "Checking prerequisites..."

if [ ! -f "Directory.Build.props" ]; then
    print_error "❌ Directory.Build.props not found. Run from repository root."
    exit 1
fi

if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    print_status "✅ .NET SDK found: $DOTNET_VERSION"
else
    print_error "❌ .NET SDK not found"
    exit 1
fi

echo ""

# Start containers if requested
if [ "$START_NEO4J" = true ]; then
    print_header "Starting Neo4j container..."
    if ./scripts/containers/start-neo4j.sh; then
        print_status "✅ Neo4j container started"
        # Wait for Neo4j to be ready
        print_status "Waiting for Neo4j to be ready..."
        sleep 10
    else
        print_error "❌ Failed to start Neo4j container"
        exit 1
    fi
fi

if [ "$START_SEQ" = true ]; then
    print_header "Starting Seq container..."
    if ./scripts/containers/start-seq.sh; then
        print_status "✅ Seq container started"
    else
        print_error "❌ Failed to start Seq container"
        exit 1
    fi
fi

echo ""

# Build the solution
print_header "Building solution..."
print_status "Configuration: $CONFIGURATION"
print_status "Verbosity: $VERBOSITY"

# Set up local feed if needed for Release configuration
if [ "$CONFIGURATION" = "Release" ]; then
    print_status "Setting up local feed for Release configuration..."
    dotnet build --configuration LocalFeed --no-restore --verbosity quiet
fi

# Build the solution
if dotnet build --configuration "$CONFIGURATION" --no-restore --verbosity "$VERBOSITY"; then
    print_status "✅ Build successful"
else
    print_error "❌ Build failed"
    exit 1
fi

echo ""

# Run analyzer tests
if [ "$RUN_ANALYZERS" = true ]; then
    print_header "Running Analyzer Tests..."
    if dotnet test --project tests/Graph.Model.Analyzers.Tests/Graph.Model.Analyzers.Tests.csproj \
        --configuration "$CONFIGURATION" \
        --no-build \
        --verbosity "$VERBOSITY" \
        --logger "console;verbosity=normal"; then
        print_status "✅ Analyzer tests passed"
    else
        print_error "❌ Analyzer tests failed"
        exit 1
    fi
    echo ""
fi

# Run Neo4j tests
if [ "$RUN_NEO4J" = true ]; then
    print_header "Running Neo4j Tests..."
    
    # Set environment variable for container tests
    export USE_NEO4J_CONTAINERS=true
    
    if dotnet test --project tests/Graph.Model.Neo4j.Tests/Graph.Model.Neo4j.Tests.csproj \
        --configuration "$CONFIGURATION" \
        --no-build \
        --verbosity "$VERBOSITY" \
        --logger "console;verbosity=normal"; then
        print_status "✅ Neo4j tests passed"
    else
        print_error "❌ Neo4j tests failed"
        exit 1
    fi
    echo ""
fi

# Run performance tests
if [ "$RUN_PERFORMANCE" = true ]; then
    print_header "Running Performance Tests..."
    if ./scripts/run-benchmarks.sh; then
        print_status "✅ Performance tests completed"
    else
        print_error "❌ Performance tests failed"
        exit 1
    fi
    echo ""
fi

# Collect coverage if requested
if [ "$COLLECT_COVERAGE" = true ]; then
    print_header "Collecting Code Coverage..."
    
    # Check if coverlet is available
    if ! dotnet tool list --global | grep -q coverlet; then
        print_status "Installing coverlet.collector..."
        dotnet tool install --global coverlet.collector
    fi
    
    # Run tests with coverage
    print_status "Running tests with coverage collection..."
    
    # Create coverage directory
    mkdir -p coverage
    
    # Run tests with coverage
    if dotnet test --configuration "$CONFIGURATION" \
        --no-build \
        --verbosity "$VERBOSITY" \
        --collect:"XPlat Code Coverage" \
        --results-directory coverage; then
        print_status "✅ Coverage collection completed"
        print_status "📊 Coverage reports available in: coverage/"
    else
        print_error "❌ Coverage collection failed"
        exit 1
    fi
fi

echo ""
print_header "🎉 All tests completed successfully!"
print_status "Configuration: $CONFIGURATION"
print_status "Verbosity: $VERBOSITY"

if [ "$COLLECT_COVERAGE" = true ]; then
    print_status "Coverage reports: coverage/"
fi

if [ "$RUN_PERFORMANCE" = true ]; then
    print_status "Performance results: benchmarks/"
fi 