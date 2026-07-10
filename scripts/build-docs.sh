#!/bin/bash

# CVOYA graph Documentation Builder
# Builds all source projects and copies XML documentation to docs/api folder

set -e

echo "🔨 Building CVOYA graph documentation..."

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

# Create docs/api directory if it doesn't exist
echo ""
echo "📁 Creating docs/api directory..."
mkdir -p docs/api

# Copy XML documentation files from build outputs
echo "📋 Copying XML documentation files..."
copied_count=0

for project in src/*/; do
    if [ -f "$project"*.csproj ]; then
        project_name=$(basename "$project")
        
        # Look for XML files in the build output
        xml_files=$(find "$project" -name "*.xml" -path "*/bin/$CONFIG/*" 2>/dev/null || true)
        
        for xml_file in $xml_files; do
            if [ -f "$xml_file" ]; then
                filename=$(basename "$xml_file")
                cp "$xml_file" "docs/api/$filename"
                echo "  📄 Copied $filename"
                copied_count=$((copied_count + 1))
            fi
        done
    fi
done

# Verify XML files were copied
echo ""
echo "📚 Generated XML documentation files:"
if [ -d "docs/api" ] && [ "$(ls -A docs/api/*.xml 2>/dev/null)" ]; then
    ls -la docs/api/*.xml | while read -r line; do
        filename=$(basename "$(echo "$line" | awk '{print $9}')")
        size=$(echo "$line" | awk '{print $5}')
        echo "  ✅ $filename ($size bytes)"
    done
    echo "  📊 Total files copied: $copied_count"
else
    echo "  ❌ No XML documentation files found"
    echo "  💡 Make sure projects have <GenerateDocumentationFile>true</GenerateDocumentationFile>"
    exit 1
fi

echo ""
echo "✅ Documentation build completed!"
echo "📂 XML files available in: docs/api/"
echo ""
echo "💡 Usage:"
echo "   - GitHub Pages: Jekyll will process files automatically"
echo "   - Local testing: bundle exec jekyll serve (requires Jekyll setup)"
echo "   - DocFX: Use XML files in docs/api/ as input" 