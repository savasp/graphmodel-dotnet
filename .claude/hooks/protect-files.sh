#!/bin/bash
# Prevent agents from editing protected files (VERSION, .csproj, CI config).
# Exit 2 = block the tool call; exit 0 = allow.

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // .tool_input.file_path // empty')

if [ -z "$FILE_PATH" ]; then
  exit 0
fi

PROTECTED_PATTERNS=(
  "VERSION"
  ".github/"
  "Directory.Build.props"
  "Directory.Packages.props"
  "nuget.config"
)

for pattern in "${PROTECTED_PATTERNS[@]}"; do
  if [[ "$FILE_PATH" == *"$pattern"* ]]; then
    echo "Blocked: $FILE_PATH is a protected file. Ask the user before modifying." >&2
    exit 2
  fi
done

exit 0
