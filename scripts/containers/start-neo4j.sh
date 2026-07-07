#!/bin/bash

# Neo4j Service Deployment Script
# Works with both Docker and Podman

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
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

# Detect a usable container runtime. Prefer Podman for local development, but
# allow callers to force either runtime with CONTAINER_RUNTIME=podman|docker.
RUNTIME=""
RUNTIME_ERRORS=()

try_runtime() {
    local candidate="$1"

    if ! command -v "$candidate" &> /dev/null; then
        RUNTIME_ERRORS+=("$candidate: command not found")
        return 1
    fi

    local error_output
    if error_output=$("$candidate" info 2>&1 > /dev/null); then
        RUNTIME="$candidate"
        return 0
    fi

    RUNTIME_ERRORS+=("$candidate: ${error_output%%$'\n'*}")
    return 1
}

if [ -n "${CONTAINER_RUNTIME:-}" ]; then
    case "$CONTAINER_RUNTIME" in
        docker|podman)
            try_runtime "$CONTAINER_RUNTIME" || true
            ;;
        *)
            print_error "CONTAINER_RUNTIME must be 'podman' or 'docker'."
            exit 1
            ;;
    esac
else
    try_runtime podman || try_runtime docker || true
fi

if [ -z "$RUNTIME" ]; then
    print_error "No usable container runtime found. Install/start Podman or Docker, or set CONTAINER_RUNTIME."
    for runtime_error in "${RUNTIME_ERRORS[@]}"; do
        print_warning "$runtime_error"
    done
    exit 1
fi

print_status "Using container runtime: $RUNTIME"

# Configuration
CONTAINER_NAME="neo4j"
IMAGE="neo4j:5-enterprise"

print_status "Deploying Neo4j service..."

# Check if container already exists
if $RUNTIME ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    print_warning "Container $CONTAINER_NAME already exists. Stopping and removing..."
    $RUNTIME stop "$CONTAINER_NAME" 2>/dev/null || true
    $RUNTIME rm "$CONTAINER_NAME" 2>/dev/null || true
fi

# Start container
print_status "Starting Neo4j container..."
$RUNTIME run -d \
    --name "$CONTAINER_NAME" \
    --publish 7474:7474 \
    --publish 7687:7687 \
    --env NEO4J_AUTH=neo4j/password \
    --env NEO4J_ACCEPT_LICENSE_AGREEMENT=yes \
    --env NEO4J_PLUGINS='["apoc"]' \
    --env NEO4J_dbms_security_procedures_unrestricted='apoc.*' \
    --env NEO4J_dbms_security_procedures_allowlist='apoc.*' \
    "$IMAGE"

# Wait for Neo4j to be ready
print_status "Waiting for Neo4j to be ready..."
MAX_ATTEMPTS=60
ATTEMPT=0
until $RUNTIME exec "$CONTAINER_NAME" neo4j status 2>/dev/null | grep -q "is running" || [ $ATTEMPT -ge $MAX_ATTEMPTS ]; do
    ATTEMPT=$((ATTEMPT + 1))
    if [ $((ATTEMPT % 10)) -eq 0 ]; then
        print_status "Still waiting for Neo4j... (attempt $ATTEMPT/$MAX_ATTEMPTS)"
    fi
    sleep 2
done

if [ $ATTEMPT -ge $MAX_ATTEMPTS ]; then
    print_error "Neo4j did not become ready in time"
    $RUNTIME logs "$CONTAINER_NAME"
    exit 1
fi

port_is_open() {
    local port="$1"

    if command -v nc &> /dev/null; then
        nc -z localhost "$port" &> /dev/null
    else
        (echo > "/dev/tcp/localhost/$port") &> /dev/null
    fi
}

# Also wait for exposed ports to accept connections.
print_status "Waiting for Neo4j HTTP endpoint..."
ATTEMPT=0
until curl -s -o /dev/null http://localhost:7474 || [ $ATTEMPT -ge 30 ]; do
    ATTEMPT=$((ATTEMPT + 1))
    sleep 2
done

if [ $ATTEMPT -ge 30 ]; then
    print_error "Neo4j HTTP endpoint did not become ready in time"
    $RUNTIME logs "$CONTAINER_NAME"
    exit 1
fi

print_status "Waiting for bolt port..."
ATTEMPT=0
until port_is_open 7687 || [ $ATTEMPT -ge 30 ]; do
    ATTEMPT=$((ATTEMPT + 1))
    sleep 2
done

if [ $ATTEMPT -ge 30 ]; then
    print_error "Neo4j bolt port did not become ready in time"
    $RUNTIME logs "$CONTAINER_NAME"
    exit 1
fi

print_status "Neo4j service deployed successfully!"
print_status "Access Neo4j browser at: http://localhost:7474"
print_status "Bolt connection: bolt://localhost:7687"
print_status "Test credentials: NEO4J_URI=bolt://localhost:7687 NEO4J_USER=neo4j NEO4J_PASSWORD=password"
