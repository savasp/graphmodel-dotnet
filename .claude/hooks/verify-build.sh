#!/bin/bash
# After editing a .cs file, do a quick build check.
# Non-zero exit (other than 2) is logged but doesn't block.

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Only check C# source files
if [[ "$FILE_PATH" != *.cs ]]; then
  exit 0
fi

# Find the nearest .csproj to build just the affected project
DIR=$(dirname "$FILE_PATH")
while [ "$DIR" != "/" ]; do
  CSPROJ=$(find "$DIR" -maxdepth 1 -name "*.csproj" -print -quit 2>/dev/null)
  if [ -n "$CSPROJ" ]; then
    dotnet build "$CSPROJ" --configuration Debug --no-restore --verbosity quiet 2>&1
    exit $?
  fi
  DIR=$(dirname "$DIR")
done

exit 0
