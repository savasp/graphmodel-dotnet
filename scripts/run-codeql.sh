#!/bin/bash

# CVOYA graph CodeQL Runner
# Runs local C# CodeQL analysis using the same query suite as the GitHub workflow.

set -euo pipefail

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

print_database_create_failure_guidance() {
    if [ "$BUILD_MODE" = "manual" ]; then
        print_warning "Manual build mode relies on CodeQL compiler tracing for the local platform and toolchain."
        print_warning "If the build completed but CodeQL reported that no C# source was processed, rerun './scripts/run-codeql.sh' without '--build-mode manual' for the portable local gate."

        if [ -f "$DATABASE_DIR/log/build-tracer.log" ]; then
            print_warning "Compiler tracing diagnostics: $DATABASE_DIR/log/build-tracer.log"
        fi
    fi
}

usage() {
    cat <<'EOF'
CVOYA graph CodeQL Runner

Usage: ./scripts/run-codeql.sh [options]

Options:
  -o, --output-dir <dir>       SARIF output directory (default: artifacts/codeql)
  --build-mode <mode>          CodeQL build mode: none or manual (default: none; manual is optional and tracer-dependent)
  --no-download                Do not download/update the CodeQL C# query pack
  --fail-on-alerts             Exit non-zero on error/warning results (default)
  --allow-alerts               Report results without failing
  --threads <count>            Number of CodeQL evaluator threads
  --ram <mb>                   Maximum RAM for CodeQL, in MB
  -h, --help                   Show this help message

Examples:
  ./scripts/run-codeql.sh
  ./scripts/run-codeql.sh --build-mode manual
  ./scripts/run-codeql.sh --allow-alerts
  ./scripts/run-codeql.sh --threads 4 --ram 8192
EOF
}

OUTPUT_DIR="artifacts/codeql"
BUILD_MODE="none"
DOWNLOAD_QUERIES=true
FAIL_ON_ALERTS=true
BUILD_ONLY=false
THREADS=""
RAM=""
QUERY_PACK="codeql/csharp-queries"
QUERY_SUITE="codeql/csharp-queries:codeql-suites/csharp-security-and-quality.qls"
TEMP_SOURCE_ROOT=""
TEMP_DATABASE_ROOT=""

cleanup() {
    if [ -n "$TEMP_SOURCE_ROOT" ]; then
        rm -rf "$TEMP_SOURCE_ROOT"
    fi

    if [ -n "$TEMP_DATABASE_ROOT" ]; then
        rm -rf "$TEMP_DATABASE_ROOT"
    fi
}

trap cleanup EXIT

run_codeql_build() {
    dotnet build-server shutdown || true
    dotnet clean --configuration LocalFeed --verbosity quiet
    dotnet clean --configuration Release --verbosity quiet
    dotnet build --configuration LocalFeed --no-restore --no-incremental --verbosity minimal -p:UseSharedCompilation=false
    dotnet build --configuration Release --no-restore --no-incremental --verbosity minimal -p:UseSharedCompilation=false
}

while [[ $# -gt 0 ]]; do
    case $1 in
        -o|--output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --build-mode)
            BUILD_MODE="$2"
            shift 2
            ;;
        --no-download)
            DOWNLOAD_QUERIES=false
            shift
            ;;
        --fail-on-alerts)
            FAIL_ON_ALERTS=true
            shift
            ;;
        --allow-alerts)
            FAIL_ON_ALERTS=false
            shift
            ;;
        --build-only)
            BUILD_ONLY=true
            shift
            ;;
        --threads)
            THREADS="$2"
            shift 2
            ;;
        --ram)
            RAM="$2"
            shift 2
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

if [ "$BUILD_ONLY" = true ]; then
    run_codeql_build
    exit 0
fi

SCAN_ROOT="$PWD"

case "$BUILD_MODE" in
    none|manual)
        ;;
    *)
        print_error "Unsupported build mode: $BUILD_MODE"
        usage
        exit 1
        ;;
esac

DATABASE_DIR="$OUTPUT_DIR/db/csharp"
RESULTS_DIR="$OUTPUT_DIR/results"
RESULTS_FILE="$RESULTS_DIR/csharp.sarif"

print_header "CodeQL C# Analysis"
echo ""

print_status "Checking prerequisites..."

if [ ! -f "Directory.Build.props" ]; then
    print_error "Directory.Build.props not found. Run from repository root."
    exit 1
fi

if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    print_status ".NET SDK found: $DOTNET_VERSION"
else
    print_error ".NET SDK not found"
    exit 1
fi

if command -v codeql &> /dev/null; then
    CODEQL_VERSION=$(codeql version | head -n 1)
    print_status "CodeQL CLI found: $CODEQL_VERSION"
else
    print_error "CodeQL CLI not found. Install it from https://github.com/github/codeql-cli-binaries/releases."
    exit 1
fi

if ! command -v jq &> /dev/null; then
    print_error "jq is required to evaluate CodeQL result severities."
    exit 1
fi

if [ "$BUILD_MODE" = "none" ] && ! command -v rsync &> /dev/null; then
    print_error "rsync is required for build-mode none because CodeQL runs in a disposable source copy."
    exit 1
fi

echo ""

export DOTNET_NOLOGO=true
export DOTNET_CLI_TELEMETRY_OPTOUT=true
export ContinuousIntegrationBuild=true

if git rev-parse --verify HEAD >/dev/null 2>&1; then
    export SourceRevisionId
    SourceRevisionId=$(git rev-parse HEAD)
fi

if [ "$BUILD_MODE" = "none" ]; then
    TEMP_SOURCE_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/graphmodel-codeql-source.XXXXXX")
    TEMP_DATABASE_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/graphmodel-codeql-db.XXXXXX")
    DATABASE_DIR="$TEMP_DATABASE_ROOT/csharp"
    print_header "Preparing disposable source copy..."
    print_status "Source copy: $TEMP_SOURCE_ROOT"
    print_status "Database work directory: $DATABASE_DIR"
    rsync -a --delete \
        --exclude='.git/' \
        --exclude='artifacts/' \
        --exclude='bin/' \
        --exclude='obj/' \
        --exclude='local-nuget-feed/' \
        --exclude='TestResults/' \
        "$PWD"/ "$TEMP_SOURCE_ROOT"/
    SCAN_ROOT="$TEMP_SOURCE_ROOT"
    echo ""
fi

mkdir -p "$(dirname "$DATABASE_DIR")" "$RESULTS_DIR"

if [ "$DOWNLOAD_QUERIES" = true ]; then
    print_header "Preparing CodeQL query pack..."
    codeql pack download "$QUERY_PACK"
    echo ""
fi

if [ "$BUILD_MODE" = "manual" ]; then
    print_header "Restoring dependencies..."
    dotnet restore
    echo ""
fi

BUILD_COMMAND="./scripts/run-codeql.sh --build-only"

CREATE_ARGS=(
    database create
    "$DATABASE_DIR"
    --language=csharp
    --source-root="$SCAN_ROOT"
    --overwrite
)

case "$BUILD_MODE" in
    none)
        CREATE_ARGS+=(--build-mode=none)
        ;;
    manual)
        CREATE_ARGS+=(--command="$BUILD_COMMAND")
        ;;
esac

ANALYZE_ARGS=(
    database analyze
    "$DATABASE_DIR"
    "$QUERY_SUITE"
    --format=sarif-latest
    --output="$RESULTS_FILE"
    --sarif-category=/language:csharp
)

if [ "$DOWNLOAD_QUERIES" = true ]; then
    ANALYZE_ARGS+=(--download)
fi

if [ -n "$THREADS" ]; then
    CREATE_ARGS+=(--threads="$THREADS")
    ANALYZE_ARGS+=(--threads="$THREADS")
fi

if [ -n "$RAM" ]; then
    CREATE_ARGS+=(--ram="$RAM")
    ANALYZE_ARGS+=(--ram="$RAM")
fi

print_header "Creating CodeQL database..."
print_status "Database: $DATABASE_DIR"
print_status "Build mode: $BUILD_MODE"
CREATE_EXIT=0
codeql "${CREATE_ARGS[@]}" || CREATE_EXIT=$?

if [ "$CREATE_EXIT" -ne 0 ]; then
    print_database_create_failure_guidance
    exit "$CREATE_EXIT"
fi
echo ""

print_header "Analyzing CodeQL database..."
print_status "Results: $RESULTS_FILE"
codeql "${ANALYZE_ARGS[@]}"
echo ""

RESULT_LEVELS_JQ='[
    .runs[] as $run
    | $run.results[]? as $result
    | ($result.level
        // ([
          ($run.tool.driver.rules[]?),
          ($run.tool.extensions[]?.rules[]?)
          | select(.id == $result.ruleId)
          | .defaultConfiguration.level
        ][0])
        // "warning")
]'

RESULT_COUNT=$(jq '[.runs[].results[]?] | length' "$RESULTS_FILE")
RESULT_SUMMARY=$(jq -r "$RESULT_LEVELS_JQ | sort | group_by(.) | map(\"\(.[0])=\(length)\") | join(\", \")" "$RESULTS_FILE")
GATING_RESULT_COUNT=$(jq "$RESULT_LEVELS_JQ | map(select(. == \"error\" or . == \"warning\")) | length" "$RESULTS_FILE")

if [ -z "$RESULT_SUMMARY" ]; then
    RESULT_SUMMARY="none"
fi

print_status "CodeQL SARIF results: $RESULT_COUNT ($RESULT_SUMMARY)"

if [ "$FAIL_ON_ALERTS" = true ] && [ "$GATING_RESULT_COUNT" -gt 0 ]; then
    print_error "CodeQL produced $GATING_RESULT_COUNT error/warning result(s). See $RESULTS_FILE."
    exit 2
fi

print_status "CodeQL analysis completed successfully."
print_status "SARIF: $RESULTS_FILE"
