#!/bin/bash

# GraphModel Documentation Builder
# Builds all source projects and copies XML documentation to docs/api folder

set -e

echo "🔨 Building GraphModel documentation..."

# Configuration to use (default: Release for documentation)
CONFIG=${1:-Debug}

echo "📋 Using configuration: $CONFIG"

# Build all source projects to generate XML documentation
echo "🏗️  Building source projects..."

for project in src/*/; do
    if [ -f "$project"*.csproj ]; then
        project_name=$(basename "$project")
        echo "  📦 Building $project_name..."
        dotnet build "$project" --configuration "$CONFIG" --no-restore --verbosity quiet
    fi
done

# Verify XML files were copied
echo ""
echo "📚 Generated XML documentation files:"
if [ -d "docs/api" ]; then
    ls -la docs/api/*.xml | while read -r line; do
        filename=$(basename "$(echo "$line" | awk '{print $9}')")
        size=$(echo "$line" | awk '{print $5}')
        echo "  ✅ $filename ($size bytes)"
    done
else
    echo "  ❌ No docs/api directory found"
    exit 1
fi

echo ""
echo "✅ Documentation build completed!"
echo "📂 XML files available in: docs/api/"
echo ""
echo "💡 Usage with documentation generators:"
echo "   - DocFX: Use these XML files as input"
echo "   - Sandcastle: Reference the XML files"
echo "   - Custom tools: Parse XML for API documentation" 