#!/bin/bash

# CVOYA graph Local NuGet Feed Cleanup
# Delegates to the repository-level package-validation orchestrator.

set -e

echo "🧹 Cleaning up CVOYA graph local NuGet feed..."

dotnet msbuild eng/PackageValidation.proj -target:Clean

echo ""
echo "✅ Cleanup complete!"
echo "🔄 You can now build normally with project references."
