#!/usr/bin/env bash
# PreToolUse guard for Edit/Write: blocks edits to protected files so the agent
# asks the user first. Reads the tool-call JSON from stdin; exit 2 + stderr =
# block (the message is fed back to the agent), exit 0 = allow.
#
# This is ADVISORY, not a security boundary — Bash commands are not intercepted.
# Protected paths (relative to the repo root):
#   VERSION, .github/**, Directory.Build.props, Directory.Packages.props,
#   nuget.config, .claude/**, .codex/**
set -u

input="$(cat)"

# Fail open: a missing jq must not wedge the session.
if ! command -v jq >/dev/null 2>&1; then
  printf 'protect-files: jq not found, guard skipped\n' >&2
  exit 0
fi

path="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
[ -z "$path" ] && exit 0

# Normalize to a repo-relative path so patterns anchor at the repo root
# instead of substring-matching anywhere in the path.
root="${CLAUDE_PROJECT_DIR:-$(pwd)}"
rel="${path#"$root"/}"

case "$rel" in
  VERSION|.github/*|Directory.Build.props|Directory.Packages.props|nuget.config|.claude/*|.codex/*)
    printf 'Blocked: %s is a protected file (see AGENTS.md "Shared-file discipline"). Ask the user before modifying it.\n' "$rel" >&2
    exit 2
    ;;
esac

exit 0
