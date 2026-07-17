#!/bin/bash

# CVOYA graph Test Runner
# Discovers repository test projects and runs the requested test lane.

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

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

usage() {
    cat <<'EOF'
CVOYA graph Test Runner

Usage: ./scripts/run-tests.sh [options]

Options:
  -c, --configuration <config>  Build configuration (default: Debug)
  -v, --verbosity <level>       Build/test verbosity (default: normal)
  --lane <fast|neo4j|age|all>   Test lane (default: all)
  --fast                        Alias for --lane fast
  --coverage                    Collect Cobertura coverage per test project
  --neo4j                       Start the local Neo4j container before tests
  --age                         Start the local Apache AGE container before tests
  --seq                         Start the local Seq container before tests
  --no-analyzers                Exclude analyzer tests
  --no-neo4j                    Exclude Neo4j tests from the all lane
  --no-age                      Exclude AGE tests from the all lane
  --no-build                    Reuse an existing build
  --disable-diff-engine         Keep Verify snapshot failures in terminal output
  --performance                 Run benchmarks after tests
  -h, --help                    Show this help message

Examples:
  ./scripts/run-tests.sh --fast
  ./scripts/run-tests.sh --lane neo4j --neo4j
  ./scripts/run-tests.sh --lane age --age
  ./scripts/run-tests.sh --neo4j --age
  ./scripts/run-tests.sh --fast --disable-diff-engine
EOF
}

CONFIGURATION="Debug"
VERBOSITY="normal"
LANE="all"
COLLECT_COVERAGE=false
START_NEO4J=false
START_AGE=false
START_SEQ=false
RUN_ANALYZERS=true
RUN_NEO4J=true
RUN_AGE=true
RUN_PERFORMANCE=false
BUILD_SOLUTION=true
DISABLE_DIFF_ENGINE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="${2:?--configuration requires a value}"
            shift 2
            ;;
        -v|--verbosity)
            VERBOSITY="${2:?--verbosity requires a value}"
            shift 2
            ;;
        --lane)
            LANE="${2:?--lane requires a value}"
            shift 2
            ;;
        --fast)
            LANE="fast"
            shift
            ;;
        --coverage)
            COLLECT_COVERAGE=true
            shift
            ;;
        --neo4j)
            START_NEO4J=true
            shift
            ;;
        --age)
            START_AGE=true
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
        --no-age)
            RUN_AGE=false
            shift
            ;;
        --no-build)
            BUILD_SOLUTION=false
            shift
            ;;
        --disable-diff-engine)
            DISABLE_DIFF_ENGINE=true
            shift
            ;;
        --performance)
            RUN_PERFORMANCE=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            exit 64
            ;;
    esac
done

case "$LANE" in
    fast|neo4j|age|all) ;;
    *)
        print_error "--lane must be fast, neo4j, age, or all."
        exit 64
        ;;
esac

if [ "$START_NEO4J" = true ] && [ "$RUN_NEO4J" = false ]; then
    print_error "--neo4j and --no-neo4j cannot be combined."
    exit 64
fi

if [ "$START_AGE" = true ] && [ "$RUN_AGE" = false ]; then
    print_error "--age and --no-age cannot be combined."
    exit 64
fi

print_header "CVOYA graph test runner"
print_status "Configuration: $CONFIGURATION"
print_status "Lane: $LANE"

if [ ! -f "cvoya-graph.sln" ] || [ ! -d "tests" ]; then
    print_error "Run this script from the repository root."
    exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
    print_error ".NET SDK not found."
    exit 1
fi

print_status ".NET SDK: $(dotnet --version)"

if [ "$START_NEO4J" = true ]; then
    print_header "Starting Neo4j"
    ./scripts/containers/start-neo4j.sh
    export NEO4J_URI="${NEO4J_URI:-bolt://localhost:7687}"
    export NEO4J_USER="${NEO4J_USER:-neo4j}"
    export NEO4J_PASSWORD="${NEO4J_PASSWORD:-password}"
fi

if [ "$START_AGE" = true ]; then
    print_header "Starting Apache AGE"
    ./scripts/containers/start-age.sh
    export AGE_CONNECTION_STRING="${AGE_CONNECTION_STRING:-Host=localhost;Port=${AGE_PORT:-5455};Username=postgres;Password=postgres;Database=postgres}"
fi

if [ "$START_SEQ" = true ]; then
    print_header "Starting Seq"
    ./scripts/containers/start-seq.sh
fi

if [ "$DISABLE_DIFF_ENGINE" = true ]; then
    export DiffEngine_Disabled=true
fi

if [ "$BUILD_SOLUTION" = true ]; then
    print_header "Building solution"
    dotnet build cvoya-graph.sln \
        --configuration "$CONFIGURATION" \
        --verbosity "$VERBOSITY"
fi

should_run_project() {
    local project_name="$1"

    case "$project_name" in
        Graph.Performance.Tests.csproj)
            return 1
            ;;
        Graph.Analyzers.Tests.csproj)
            [ "$RUN_ANALYZERS" = true ] || return 1
            ;;
        Graph.Neo4j.Tests.csproj)
            [ "$RUN_NEO4J" = true ] || return 1
            case "$LANE" in
                neo4j|all) return 0 ;;
                *) return 1 ;;
            esac
            ;;
        Graph.Age.Tests.csproj)
            [ "$RUN_AGE" = true ] || return 1
            case "$LANE" in
                age|all) return 0 ;;
                *) return 1 ;;
            esac
            ;;
    esac

    case "$LANE" in
        fast|all) return 0 ;;
        *) return 1 ;;
    esac
}

run_test_project() {
    local project="$1"
    local project_name
    local slug
    local log_file
    local test_count
    local -a command

    project_name=$(basename "$project" .csproj)
    slug=$(printf '%s' "$project_name" | tr '[:upper:]' '[:lower:]' | tr '.' '-')
    log_file=$(mktemp "${TMPDIR:-/tmp}/cvoya-test-output.XXXXXX")
    command=(
        dotnet test
        --project "$project"
        --configuration "$CONFIGURATION"
        --no-build
        --verbosity "$VERBOSITY"
    )

    if [ "$COLLECT_COVERAGE" = true ]; then
        command+=(
            --results-directory "coverage/$slug"
            --coverage
            --coverage-output "$slug.cobertura.xml"
            --coverage-output-format cobertura
        )
    fi

    print_header "Testing $project_name"
    if ! "${command[@]}" 2>&1 | tee "$log_file"; then
        rm -f "$log_file"
        return 1
    fi

    test_count=$(sed -nE 's/^[[:space:]]*total:[[:space:]]*([0-9]+)[[:space:]]*$/\1/p' "$log_file" | tail -n 1)
    rm -f "$log_file"

    if [ -z "$test_count" ] || [ "$test_count" -eq 0 ]; then
        print_error "$project completed without reporting a nonzero test count."
        return 1
    fi

    SELECTED_TEST_COUNT=$((SELECTED_TEST_COUNT + test_count))
    SELECTED_PROJECT_COUNT=$((SELECTED_PROJECT_COUNT + 1))
}

SELECTED_PROJECT_COUNT=0
SELECTED_TEST_COUNT=0

while IFS= read -r project; do
    project_name=$(basename "$project")
    if should_run_project "$project_name"; then
        run_test_project "$project"
    fi
done < <(find tests -name '*.csproj' -print | LC_ALL=C sort)

if [ "$SELECTED_PROJECT_COUNT" -eq 0 ]; then
    print_error "The selected lane did not contain any test projects."
    exit 1
fi

if [ "$RUN_PERFORMANCE" = true ]; then
    print_header "Running performance benchmarks"
    ./scripts/run-benchmarks.sh
fi

print_header "Test lane completed"
print_status "$SELECTED_PROJECT_COUNT project(s), $SELECTED_TEST_COUNT test(s)"

if [ "$COLLECT_COVERAGE" = true ]; then
    print_status "Coverage reports: coverage/"
fi
