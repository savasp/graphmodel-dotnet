#!/bin/bash

# GraphModel Seq Log Aggregation Setup
# Starts Seq log aggregation system for development/testing using Podman

set -e

echo "🔍 Starting Seq log aggregation system..."

# Create log data directory if it doesn't exist
LOG_DATA_DIR="${HOME}/tmp/logdata"
mkdir -p "$LOG_DATA_DIR"

# Check if container already exists
if podman ps -a --format "table {{.Names}}" | grep -q "seq"; then
    echo "🔄 Removing existing Seq container..."
    podman rm -f seq
fi

echo "🚀 Starting Seq container..."
podman run \
    --name seq \
    -d --restart unless-stopped \
    -e ACCEPT_EULA=Y \
    -e SEQ_PASSWORD="" \
    -p 5341:80 \
    -v "$LOG_DATA_DIR:/data" \
    datalust/seq:latest

echo "✅ Seq started successfully!"
echo "📊 UI available at: http://localhost:5341"
echo "📁 Log data stored in: $LOG_DATA_DIR"
echo ""
echo "💡 To stop: podman stop seq"
echo "💡 To remove: podman rm seq"
