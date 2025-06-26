#!/bin/bash

# GraphModel Local NuGet Feed Cleanup
# Uses MSBuild target for consistent cleanup

set -e

echo "🧹 Cleaning up GraphModel local NuGet feed..."

# Use MSBuild target for cleanup
dotnet msbuild -target:CleanLocalFeed

echo ""
echo "✅ Cleanup complete!"
echo "🔄 You can now build normally with project references." 