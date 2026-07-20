#!/usr/bin/env bash
# Local pre-push gate — the fast subset of the CI static checks for cvoya-graph.
#
# Run automatically by the pre-push hook (.githooks/pre-push); install once per
# clone (and per worktree) with eng/install-hooks.sh. It mirrors the fast, static
# CI checks — a Release build (warnings-as-errors) and the format verification —
# scoped by change detection so it only runs when .NET sources actually change,
# catching an obvious break before it burns a PR / merge-queue cycle.
#
# The full test matrix (unit + Neo4j + Apache AGE integration suites) runs in CI
# on the PR and again in the merge queue — those need containers, so they stay in
# CI. Pass --full to also run the fast unit lane locally.
#
# Usage:
#   eng/ci/ci-local.sh [--all] [--full] [-h|--help]
#     (no flags)  auto-detect: run only when .NET sources changed vs origin/main
#     --all       run regardless of detected changes
#     --full      also run the fast unit-test lane (scripts/run-tests.sh --lane fast)
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

if [ -t 1 ]; then
  RED=$'\033[0;31m'; GREEN=$'\033[0;32m'; BLUE=$'\033[0;34m'; NC=$'\033[0m'
else
  RED=""; GREEN=""; BLUE=""; NC=""
fi
say() { echo "${BLUE}[ci-local]${NC} $*"; }
ok()  { echo "${GREEN}[ok]${NC} $*"; }
err() { echo "${RED}[fail]${NC} $*" >&2; }

FULL=0 FORCE_ALL=0
while [ $# -gt 0 ]; do
  case "$1" in
    --full) FULL=1 ;;
    --all)  FORCE_ALL=1 ;;
    -h|--help) awk '/^# Local pre-push gate/{p=1} p&&!/^#/{exit} p&&/^#/{sub(/^# ?/,"");print}' "$0"; exit 0 ;;
    *) err "unknown argument: $1"; exit 2 ;;
  esac
  shift
done

# Decide whether the .NET gate must run for this change set. Mirrors the `dotnet`
# path filter in .github/workflows/ci.yml, plus the gate/hook scripts themselves.
dotnet_changed() {
  [ "$FORCE_ALL" = 1 ] && return 0
  local base="origin/main"
  git rev-parse --verify --quiet "${base}^{commit}" >/dev/null 2>&1 || return 0  # no base → run
  local files
  files="$( { git diff --name-only "${base}...HEAD" 2>/dev/null
              git diff --name-only HEAD 2>/dev/null
              git diff --name-only --cached 2>/dev/null; } | sort -u )"
  [ -n "$files" ] || return 0  # empty change set is suspicious for a gate → run, don't pass trivially
  printf '%s\n' "$files" | grep -qE '\.(cs|csproj|slnx?|props|targets)$|(^|/)global\.json$|(^|/)[Nn]u[Gg]et\.config$|(^|/)\.config/dotnet-tools\.json$|(^|/)\.editorconfig$|^eng/ci/|^\.githooks/' && return 0
  return 1
}

if ! dotnet_changed; then
  say "no .NET-relevant changes vs origin/main — nothing to verify."
  exit 0
fi

SLN="cvoya-graph.sln"
say "restore"
dotnet restore "$SLN" || { err "restore failed"; exit 1; }
say "build (Release, warnings-as-errors)"
dotnet build "$SLN" --configuration Release --no-restore || { err "build failed"; exit 1; }
say "format --verify-no-changes"
dotnet format "$SLN" --verify-no-changes --no-restore --verbosity minimal \
  || { err "formatting differs from .editorconfig — run 'dotnet format $SLN' and re-commit."; exit 1; }

if [ "$FULL" = 1 ]; then
  say "fast unit lane (scripts/run-tests.sh --lane fast)"
  ./scripts/run-tests.sh --configuration Debug --lane fast --disable-diff-engine \
    || { err "fast unit tests failed"; exit 1; }
fi

ok "local gate passed."
