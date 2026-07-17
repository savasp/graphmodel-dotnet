#!/bin/bash

# CVOYA graph Local NuGet Feed Setup
# Delegates to the repository-level package-validation orchestrator.

set -e

dotnet msbuild eng/PackageValidation.proj -target:PrepareLocalFeed -verbosity:minimal

echo "Local feed setup complete under artifacts/package-validation/feed."
echo "Run 'dotnet msbuild eng/PackageValidation.proj -target:BuildWithPackageReferences' to validate package references."
