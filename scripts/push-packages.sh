#!/usr/bin/env bash

set -euo pipefail

# Default NuGet source
NUGET_SOURCE_NAME="nuget.org"
NUGET_SOURCE_URL="https://api.nuget.org/v3/index.json"

# Parse arguments
API_KEY=""
PACKAGE_FOLDER="local-nuget-feed"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-key)
      API_KEY="$2"
      shift 2
      ;;
    --folder)
      PACKAGE_FOLDER="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

# Validate input
if [[ -z "$PACKAGE_FOLDER" ]]; then
  echo "❌ Error: --folder is required"
  exit 1
fi

if [[ ! -d "$PACKAGE_FOLDER" ]]; then
  echo "❌ Error: Folder '$PACKAGE_FOLDER' does not exist"
  exit 1
fi

# Check if source exists
# Normalize source check to handle casing and whitespace
SOURCE_EXISTS=$(dotnet nuget list source --format short | awk '{print tolower($1)}' | grep -Fx "nuget.org" || true)

if [[ -n "$API_KEY" ]]; then
  echo "🔄 Reconfiguring NuGet source with provided API key..."
  dotnet nuget remove source "$NUGET_SOURCE_NAME" 2>/dev/null || true
  dotnet nuget add source "$NUGET_SOURCE_URL" \
    --name "$NUGET_SOURCE_NAME" \
    --username "anyvalue" \
    --password "$API_KEY" \
    --store-password-in-clear-text
elif [[ -z "$SOURCE_EXISTS" ]]; then
  echo "🔐 NuGet source '$NUGET_SOURCE_NAME' is not configured."
  read -rsp "Enter NuGet API key: " API_KEY
  echo
  dotnet nuget remove source "$NUGET_SOURCE_NAME" 2>/dev/null || true
  dotnet nuget add source "$NUGET_SOURCE_URL" \
    --name "$NUGET_SOURCE_NAME" \
    --username "anyvalue" \
    --password "$API_KEY" \
    --store-password-in-clear-text
else
  echo "✅ NuGet source '$NUGET_SOURCE_NAME' already configured."
fi

# Push .nupkg files from the specified folder
echo "🚀 Pushing packages from '$PACKAGE_FOLDER'..."
for nupkg in "$PACKAGE_FOLDER"/*.nupkg; do
  echo "📦 Pushing $nupkg..."
  dotnet nuget push "$nupkg" \
    --source "$NUGET_SOURCE_URL" \
    --api-key "$API_KEY"
done

echo "✅ Done."