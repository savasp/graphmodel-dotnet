#!/usr/bin/env bash
# Point git at the version-controlled hooks in .githooks/.
# Run once per clone (and per worktree — core.hooksPath is local config).

set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

git config core.hooksPath .githooks
chmod +x .githooks/* eng/ci/ci-local.sh 2>/dev/null || true

echo "Installed git hooks: core.hooksPath=.githooks"
echo "The pre-push gate (eng/ci/ci-local.sh) now runs the fast checks before every push."
echo "Bypass in an emergency with: git push --no-verify"
