#!/bin/bash

# Neo4j Service Deployment Script
# This script deploys Neo4j using Podman

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

# Configuration
POD_NAME="neo4j-pod"
CONTAINER_NAME="neo4j"
IMAGE="neo4j:5-enterprise"

print_status "Deploying Neo4j service..."

# Check if pod already exists
if podman pod exists "$POD_NAME"; then
    print_warning "Pod $POD_NAME already exists. Stopping and removing..."
    podman pod stop "$POD_NAME" 2>/dev/null || true
    podman pod rm "$POD_NAME" 2>/dev/null || true
fi

# Create pod
print_status "Creating Neo4j pod..."
podman pod create \
    --name "$POD_NAME" \
    --network brainexpanded-internal \
    --publish 7474:7474 \
    --publish 7687:7687

# Start container
print_status "Starting Neo4j container..."
podman run -d --pod "$POD_NAME" \
    --name "$CONTAINER_NAME" \
    --env NEO4J_AUTH=neo4j/password \
    --env NEO4J_ACCEPT_LICENSE_AGREEMENT=yes \
    --env NEO4J_dbms_mode=CORE \
    --env NEO4JLABS_PLUGINS='["apoc"]' \
    --env NEO4J_dbms_security_procedures_unrestricted=apoc.* \
    --env NEO4J_dbms_security_procedures_allowlist=apoc.* \
    --volume neo4j-data:/data \
    --volume neo4j-logs:/logs \
    --restart unless-stopped \
    "$IMAGE"

print_status "Neo4j service deployed successfully!"
print_status "Access Neo4j browser at: http://localhost:7474"
print_status "Bolt connection: bolt://localhost:7687" 
