#!/bin/bash

# GraphModel Local NuGet Feed Setup (MSBuild Integration)
# Creates packages using LocalFeed configuration and sets up local feed

set -e

echo "🚀 Setting up GraphModel local NuGet feed using MSBuild integration..."
echo ""

# Clean any existing setup
echo "🧹 Cleaning previous setup..."
dotnet msbuild -target:CleanLocalFeed -verbosity:minimal

echo ""
echo "📦 Building packages with LocalFeed configuration..."
echo "   This will:"
echo "   - Use project references for fast builds"
echo "   - Apply Release-level optimizations"  
echo "   - Generate NuGet packages automatically"
echo "   - Set up local NuGet feed"
echo ""

# Build with LocalFeed configuration
dotnet build --configuration LocalFeed --verbosity minimal

echo ""
echo "✅ Local feed setup complete!"
echo ""
echo "📋 Next steps:"
echo "   1. Test Release configuration: dotnet build --configuration Release"
echo "   2. Clean up when done: dotnet msbuild -target:CleanLocalFeed"
echo ""
echo "🔍 Local feed location: $(pwd)/local-nuget-feed"
echo "📦 Packages location: $(pwd)/artifacts" 