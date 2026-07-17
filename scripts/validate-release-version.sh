#!/usr/bin/env bash

# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

set -euo pipefail

VERSION="${1:-}"
if [[ $# -ne 1 || -z "$VERSION" ]]; then
  echo "Usage: $0 <release-version>" >&2
  exit 64
fi

if ! [[ "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-(alpha|beta|rc)\.[0-9]{8}(\.[1-9][0-9]*)?)?$ ]]; then
  echo "Version '$VERSION' does not match MAJOR.MINOR.PATCH[-(alpha|beta|rc).YYYYMMDD[.N]]." >&2
  exit 1
fi

# These components become AssemblyVersion/FileVersion. ECMA-335 metadata caps
# every numeric part at UInt16.MaxValue - 1 (65534).
IFS='.' read -r MAJOR MINOR PATCH <<< "${VERSION%%-*}"
for component in "$MAJOR" "$MINOR" "$PATCH"; do
  if (( ${#component} > 5 )) || (( 10#$component > 65534 )); then
    echo "Version '$VERSION' contains a component above the .NET assembly-version maximum (65534)." >&2
    exit 1
  fi
done
