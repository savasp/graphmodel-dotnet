#!/usr/bin/env bash
# PostToolUse hook for Edit/Write: after a .cs file changes, build its project
# so compile errors reach the agent immediately. PostToolUse feedback only
# flows back on exit 2 with the message on STDERR — anything else is invisible
# to the agent, so failures must exit 2.
set -u

input="$(cat)"

# Fail open: a missing jq must not wedge the session.
command -v jq >/dev/null 2>&1 || exit 0

path="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
case "$path" in
  *.cs) ;;
  *) exit 0 ;;
esac

# Find the nearest .csproj to build just the affected project.
dir="$(dirname "$path")"
csproj=""
while [ "$dir" != "/" ] && [ -n "$dir" ]; do
  csproj="$(find "$dir" -maxdepth 1 -name '*.csproj' -print -quit 2>/dev/null)"
  [ -n "$csproj" ] && break
  dir="$(dirname "$dir")"
done
[ -z "$csproj" ] && exit 0

# No --no-restore: fresh worktrees have no restored packages yet.
output="$(dotnet build "$csproj" --configuration Debug --verbosity quiet 2>&1)"
status=$?

if [ $status -ne 0 ]; then
  {
    printf 'Build failed for %s after editing %s:\n' "$(basename "$csproj")" "$path"
    printf '%s\n' "$output" | tail -n 40
  } >&2
  exit 2
fi

exit 0
