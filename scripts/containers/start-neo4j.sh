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

# Detect container runtime
if command -v docker &> /dev/null; then
    RUNTIME="docker"
elif command -v podman &> /dev/null; then
    RUNTIME="podman"
else
    print_error "Neither docker nor podman found. Please install one of them."
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

# Also wait for bolt port to accept connections
print_status "Waiting for bolt port..."
ATTEMPT=0
until curl -s -o /dev/null http://localhost:7474 || [ $ATTEMPT -ge 30 ]; do
    ATTEMPT=$((ATTEMPT + 1))
    sleep 2
done

print_status "Neo4j service deployed successfully!"
print_status "Access Neo4j browser at: http://localhost:7474"
print_status "Bolt connection: bolt://localhost:7687"
