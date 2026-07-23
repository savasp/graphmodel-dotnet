#!/usr/bin/env bash
# Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
# See LICENSE in the project root for full license terms.

set -euo pipefail

configuration="${1:-Debug}"

ruby scripts/validate-documentation.test.rb
ruby scripts/validate-documentation.rb

example_count=0
while IFS= read -r project; do
  example_count=$((example_count + 1))
  echo "Building documentation example: $project"
  dotnet build "$project" \
    --configuration "$configuration" \
    --nologo \
    --verbosity minimal
done < <(find examples -name bin -prune -o -name obj -prune -o -name '*.csproj' -print | sort)

if [[ "$example_count" -eq 0 ]]; then
  echo "No example projects were discovered under examples/." >&2
  exit 1
fi

echo "Validated and compiled $example_count example projects."
