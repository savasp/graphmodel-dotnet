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
check "allows normal source file"             0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/src/Graph/IGraph.cs\"}}" "$P"
check "allows file merely containing VERSION" 0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/docs/VERSIONING.md\"}}" "$P"
check "allows nested Directory.Build.props"   0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/src/Directory.Build.props\"}}" "$P"
check "allows empty input"                    0 "{}" "$P"

V=.claude/hooks/verify-build.sh
check "verify-build ignores non-.cs files"    0 "{\"tool_input\":{\"file_path\":\"$CLAUDE_PROJECT_DIR/README.md\"}}" "$V"
check "verify-build ignores empty input"      0 "{}" "$V"

echo "----"
echo "$pass passed, $fail failed"
[ $fail -eq 0 ]
