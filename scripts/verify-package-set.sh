#!/bin/bash

# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

set -euo pipefail

PACKAGE_DIRECTORY="${1:-}"
EXPECTED_VERSION="${2:-$(tr -d '[:space:]' < VERSION)}"

if [ -z "$PACKAGE_DIRECTORY" ]; then
    echo "Usage: $0 <package-directory> [expected-version]" >&2
    exit 64
fi

if [ ! -d "$PACKAGE_DIRECTORY" ]; then
    echo "Package directory does not exist: $PACKAGE_DIRECTORY" >&2
    exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
    echo "jq is required to verify the package set." >&2
    exit 1
fi

expected_package_ids=""
expected_package_count=0
verification_failed=false
source_projects=()

while IFS= read -r -d '' project; do
    source_projects+=("$project")
done < <(find src -type f -name '*.csproj' -print0)

for project in "${source_projects[@]}"; do
    metadata=$(dotnet msbuild "$project" \
        -getProperty:PackageId \
        -getProperty:IsPackable \
        -getProperty:PackageVersion \
        -p:Configuration=LocalFeed)

    if [ "$(jq -r '.Properties.IsPackable' <<< "$metadata" | tr -d '\r')" != "true" ]; then
        continue
    fi

    package_id=$(jq -r '.Properties.PackageId' <<< "$metadata" | tr -d '\r')
    package_version=$(jq -r '.Properties.PackageVersion' <<< "$metadata" | tr -d '\r')
    expected_package_ids="${expected_package_ids}${package_id}"$'\n'
    expected_package_count=$((expected_package_count + 1))

    if [ "$package_version" != "$EXPECTED_VERSION" ]; then
        echo "Package version mismatch for $package_id: expected $EXPECTED_VERSION, evaluated $package_version" >&2
        verification_failed=true
    fi

    package_path="$PACKAGE_DIRECTORY/$package_id.$EXPECTED_VERSION.nupkg"
    if [ ! -f "$package_path" ]; then
        echo "Missing package for $project: $package_path" >&2
        verification_failed=true
    fi
done

actual_package_count=0
while IFS= read -r package_path; do
    package_name=$(basename "$package_path")
    package_id=${package_name%".$EXPECTED_VERSION.nupkg"}
    actual_package_count=$((actual_package_count + 1))

    if [ "$package_id" = "$package_name" ] || ! grep -Fqx "$package_id" <<< "$expected_package_ids"; then
        echo "Unexpected package: $package_path" >&2
        verification_failed=true
    fi
done < <(find "$PACKAGE_DIRECTORY" -maxdepth 1 -type f -name '*.nupkg' ! -name '*.snupkg' -print | sort)

if [ "$actual_package_count" -ne "$expected_package_count" ]; then
    echo "Package count mismatch: expected $expected_package_count, found $actual_package_count" >&2
    verification_failed=true
fi

for project in "${source_projects[@]}"; do
    package_references=$(dotnet msbuild "$project" \
        -getItem:PackageReference \
        -p:Configuration=Release \
        -p:UsePackageReferences=true \
        | jq -r '.Items.PackageReference[]?.Identity | select(startswith("Cvoya."))' \
        | tr -d '\r')

    while IFS= read -r package_reference; do
        if [ -z "$package_reference" ]; then
            continue
        fi

        if ! grep -Fqx "$package_reference" <<< "$expected_package_ids"; then
            echo "$project references internal package $package_reference, but no packable src project produces it." >&2
            verification_failed=true
        fi
    done <<< "$package_references"
done

if [ "$verification_failed" = true ]; then
    exit 1
fi

dotnet run \
    --project eng/PackageVersionVerifier/PackageVersionVerifier.csproj \
    --configuration Release \
    -- \
    "$PACKAGE_DIRECTORY" \
    "$EXPECTED_VERSION"

echo "Verified the exact $expected_package_count-package inventory in $PACKAGE_DIRECTORY at version $EXPECTED_VERSION."
