#!/bin/bash

# GraphModel DocFX Documentation Builder
# Builds comprehensive documentation site with API reference

set -e

echo "📚 Building GraphModel documentation with DocFX..."
echo ""

# Check if DocFX is installed
if ! command -v docfx &> /dev/null; then
    echo "📦 Installing DocFX..."
    dotnet tool restore
fi

# Build XML documentation first
echo "🔨 Building XML documentation..."
./scripts/build-docs.sh

# Build DocFX site
echo "🏗️ Building DocFX documentation site..."
docfx docfx.json

echo ""
echo "✅ Documentation build complete!"
echo ""
echo "📋 Next steps:"
echo "   • View locally: docfx serve _site"
echo "   • Open in browser: http://localhost:8080"
echo "   • Deploy to GitHub Pages: Push to main branch"
echo ""
echo "📂 Generated files:"
echo "   • Static site: _site/"
echo "   • XML docs: docs/api/"
echo "   • API metadata: docs/api-temp/" 