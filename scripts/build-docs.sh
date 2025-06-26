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

# Create _site directory for GitHub Pages
echo ""
echo "📁 Creating _site directory for GitHub Pages..."
rm -rf _site
mkdir -p _site

# Copy all documentation files
echo "📋 Copying documentation files to _site..."
cp -r docs/* _site/
cp README.md _site/

# Create index.html that redirects to index.md (for GitHub Pages)
echo "📄 Creating index.html redirect..."
cat > _site/index.html << 'EOF'
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>GraphModel Documentation</title>
    <meta http-equiv="refresh" content="0; url=./index.md">
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; text-align: center; padding: 50px; }
        .container { max-width: 600px; margin: 0 auto; }
        h1 { color: #333; }
        .loading { animation: pulse 1.5s ease-in-out infinite alternate; }
        @keyframes pulse { from { opacity: 1; } to { opacity: 0.5; } }
    </style>
</head>
<body>
    <div class="container">
        <h1>GraphModel Documentation</h1>
        <p class="loading">Loading documentation...</p>
        <p>If you're not redirected automatically, <a href="./index.md">click here</a>.</p>
    </div>
</body>
</html>
EOF

echo ""
echo "✅ Documentation build completed!"
echo "📂 XML files available in: docs/api/"
echo "🌐 GitHub Pages site ready in: _site/"
echo ""
echo "💡 Usage:"
echo "   - Local testing: python3 -m http.server 8000 (from _site/ directory)"
echo "   - GitHub Pages: Push changes to deploy automatically"
echo "   - DocFX: Use XML files in docs/api/ as input" 