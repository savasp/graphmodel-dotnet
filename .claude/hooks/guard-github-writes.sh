#!/usr/bin/env bash
# PreToolUse(Bash) guard: cvoya-com GitHub writes must go through the `gh-app`
# wrapper using the savasp-agent App identity. Bare git/gh writes create
# user-authored history/actions that re-trigger branch-protection self-approval
# blocks. See AGENTS.md and the github-app skill for the canonical verbs.
#
# Claude Code sends the tool payload as JSON on stdin. Exit 2 blocks the call
# and surfaces stderr to the agent; exit 0 allows it. Fail open on malformed
# input or missing jq so the advisory guard never wedges a session.

input="$(cat)"

if ! command -v jq >/dev/null 2>&1; then
  printf 'guard-github-writes: jq not found, guard skipped\n' >&2
  exit 0
fi

cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty' 2>/dev/null || true)"
[ -z "$cmd" ] && exit 0

# Strip quoted spans so messages/patterns neither trigger nor bypass the guard,
# then classify each simple command split on shell separators.
segments="$(
  printf '%s' "$cmd" \
    | sed -e "s/'[^']*'/ /g" -e 's/"[^"]*"/ /g' \
    | awk '{ gsub(/[;|&]/, "\n"); print }'
)"

blocked=""
while IFS= read -r seg; do
  seg="$(printf '%s' "$seg" | sed -E 's/^[[:space:]]+//')"
  [ -z "$seg" ] && continue
  seg="$(printf '%s' "$seg" | sed -E 's/^([A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]]+)+//; s/^(sudo|command|env|builtin)[[:space:]]+//')"

  case "$seg" in
    gh-app|gh-app[[:space:]]*|*/gh-app|*/gh-app[[:space:]]*) continue ;;
  esac

  if grep -Eq '^git[[:space:]]+(commit|push)([[:space:]]|$)' <<<"$seg"; then
    blocked="$seg"; break
  fi

  if grep -Eq '^gh[[:space:]]+(pr|issue)[[:space:]]+(create|comment|edit|merge|close|reopen|ready|review|delete|lock|unlock)([[:space:]]|$)' <<<"$seg"; then
    blocked="$seg"; break
  fi

  if grep -Eq '^gh[[:space:]]+release[[:space:]]+(create|edit|delete|upload)([[:space:]]|$)' <<<"$seg"; then
    blocked="$seg"; break
  fi

  if grep -Eq '^gh[[:space:]]+api([[:space:]]|$)' <<<"$seg"; then
    if grep -Eqi '(-X|--method)[[:space:]=]*(POST|PUT|PATCH|DELETE)' <<<"$seg" \
       || grep -Eq '(^|[[:space:]])(-f|-F|--field|--raw-field|--input)([[:space:]=]|$)' <<<"$seg"; then
      blocked="$seg"; break
    fi
  fi
done <<<"$segments"

if [ -n "$blocked" ]; then
  {
    printf 'STOP: cvoya-com GitHub writes must go through gh-app, e.g. `gh-app git commit`, `gh-app git push`, or `gh-app gh pr create`.\n'
    printf 'Blocked sub-command: %s\n' "$blocked"
    printf 'Bare git/gh writes use the wrong identity for this repository.\n'
  } >&2
  exit 2
fi

exit 0
