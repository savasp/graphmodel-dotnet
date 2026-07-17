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
#
# CLAUDE_PROJECT_DIR is the session's working directory, which is NOT always the
# repo root — a session started in scripts/ sets it to scripts/, leaving every
# path below the root unstripped and matching nothing, so the guard silently
# allowed everything. Anchor on the real root instead, and keep the env var as
# the fallback so a non-git tree still gets the old behaviour.
root="${CLAUDE_PROJECT_DIR:-$(pwd)}"
git_root="$(git -C "$root" rev-parse --show-toplevel 2>/dev/null || true)"
[ -n "$git_root" ] && root="$git_root"
canonical_root="$(cd "$root" 2>/dev/null && pwd -P || true)"
[ -n "$canonical_root" ] && root="$canonical_root"

# Resolve dot segments and symlinked parent directories before matching. Without
# this, /repo/scripts/../Directory.Build.props bypasses the root-anchored case
# even though it names the protected root file. Protected files already have an
# existing parent directory, so canonicalizing the parent also works for Write
# calls that create a new file (for example, a new workflow).
case "$path" in
  /*) ;;
  *) path="$root/$path" ;;
esac
canonical_parent="$(cd "$(dirname "$path")" 2>/dev/null && pwd -P || true)"
[ -n "$canonical_parent" ] && path="$canonical_parent/$(basename "$path")"

rel="${path#"$root"/}"

case "$rel" in
  VERSION|.github/*|Directory.Build.props|Directory.Packages.props|nuget.config|.claude/*|.codex/*)
    printf 'Blocked: %s is a protected file (see AGENTS.md "Shared-file discipline"). Ask the user before modifying it.\n' "$rel" >&2
    exit 2
    ;;
esac

exit 0
