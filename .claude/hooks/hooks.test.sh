#!/usr/bin/env bash
# Self-test for the PreToolUse/PostToolUse hooks. Run from the repo root:
#   bash .claude/hooks/hooks.test.sh
set -u
cd "$(dirname "$0")/../.." || exit 1
export CLAUDE_PROJECT_DIR="$(pwd)"

pass=0 fail=0
check() { # description expected_exit json script
  local desc="$1" expected="$2" json="$3" script="$4"
  printf '%s' "$json" | bash "$script" >/dev/null 2>&1
  local actual=$?
  if [ "$actual" -eq "$expected" ]; then
    pass=$((pass+1)); echo "PASS: $desc"
  else
    fail=$((fail+1)); echo "FAIL: $desc (expected exit $expected, got $actual)"
  fi
}

P=.claude/hooks/protect-files.sh
check "blocks VERSION (absolute path)"        2 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/VERSION\"}}" "$P"
check "blocks .github workflow"               2 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/.github/workflows/tests.yml\"}}" "$P"
check "blocks Directory.Build.props"          2 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/Directory.Build.props\"}}" "$P"
check "blocks .claude settings"               2 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/.claude/settings.json\"}}" "$P"
check "blocks .codex config"                  2 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/.codex/config.toml\"}}" "$P"
check "allows normal source file"             0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/src/Graph.Model/IGraph.cs\"}}" "$P"
check "allows file merely containing VERSION" 0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/docs/VERSIONING.md\"}}" "$P"
check "allows nested Directory.Build.props"   0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/src/Directory.Build.props\"}}" "$P"
check "allows empty input"                    0 "{}" "$P"

V=.claude/hooks/verify-build.sh
check "verify-build ignores non-.cs files"    0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/README.md\"}}" "$V"
check "verify-build ignores empty input"      0 "{}" "$V"

G=.claude/hooks/guard-github-writes.sh
check "github guard blocks bare git commit"   2 "{\"tool_input\":{\"command\":\"git commit -m \\\"wip\\\"\"}}" "$G"
check "github guard blocks bare git push"     2 "{\"tool_input\":{\"command\":\"git push origin HEAD\"}}" "$G"
check "github guard blocks gh pr create"      2 "{\"tool_input\":{\"command\":\"gh pr create --fill\"}}" "$G"
check "github guard blocks gh issue edit"     2 "{\"tool_input\":{\"command\":\"gh issue edit 70 --type Task\"}}" "$G"
check "github guard blocks gh api write"      2 "{\"tool_input\":{\"command\":\"gh api repos/o/r/issues -f title=x\"}}" "$G"
check "github guard allows gh-app commit"     0 "{\"tool_input\":{\"command\":\"gh-app git commit -m \\\"wip\\\"\"}}" "$G"
check "github guard allows gh-app push"       0 "{\"tool_input\":{\"command\":\"gh-app git push origin HEAD\"}}" "$G"
check "github guard allows gh-app pr create"  0 "{\"tool_input\":{\"command\":\"gh-app gh pr create --fill\"}}" "$G"
check "github guard allows read-only git"     0 "{\"tool_input\":{\"command\":\"git status -sb\"}}" "$G"
check "github guard allows read-only gh"      0 "{\"tool_input\":{\"command\":\"gh pr view 1\"}}" "$G"
check "github guard allows quoted mentions"   0 "{\"tool_input\":{\"command\":\"echo \\\"git commit && gh pr create\\\"\"}}" "$G"

echo "----"
echo "$pass passed, $fail failed"
[ $fail -eq 0 ]
