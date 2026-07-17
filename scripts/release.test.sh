#!/usr/bin/env bash

# Self-test for scripts/release.sh. Every external command is replaced with a
# deterministic fake, so this test never reads or writes a real Git repository,
# GitHub release, workflow, tag, or NuGet package.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_SCRIPT="$SCRIPT_DIR/release.sh"
TEST_ROOT="$(mktemp -d)"
FAKE_BIN="$TEST_ROOT/bin"
TEST_LOG="$TEST_ROOT/commands.log"
mkdir -p "$FAKE_BIN"
trap 'rm -rf "$TEST_ROOT"' EXIT

export TEST_LOG

cat > "$FAKE_BIN/date" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "${TEST_TODAY:-20260717}"
EOF

cat > "$FAKE_BIN/sleep" <<'EOF'
#!/usr/bin/env bash
exit 0
EOF

cat > "$FAKE_BIN/git" <<'EOF'
#!/usr/bin/env bash
set -u
printf 'git %s\n' "$*" >> "$TEST_LOG"

case "${1:-}" in
  remote)
    if [[ "${2:-}" == "get-url" && "${3:-}" == "--push" ]]; then
      printf '%s\n' "${TEST_ORIGIN_PUSH_URL:-https://github.com/cvoya-com/graph}"
    else
      printf '%s\n' "${TEST_ORIGIN_FETCH_URL:-https://github.com/cvoya-com/graph}"
    fi
    ;;
  status)
    [[ "${TEST_DIRTY:-false}" == "true" ]] && printf '%s\n' ' M tracked-file'
    exit 0
    ;;
  fetch)
    [[ "${TEST_FETCH_FAIL:-false}" == "true" ]] && exit 1
    exit 0
    ;;
  rev-parse)
    if [[ "${2:-}" == "origin/main" ]]; then
      printf '%s\n' "${TEST_ORIGIN_SHA:-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa}"
    else
      printf '%s\n' "${TEST_LOCAL_SHA:-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa}"
    fi
    ;;
  ls-remote)
    tag="${!#}"
    tag="${tag#refs/tags/}"
    if grep -Fqx "$tag" <<< "${TEST_REMOTE_TAGS:-}"; then
      printf '%s\n' "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\trefs/tags/$tag"
      exit 0
    fi
    exit "${TEST_LS_REMOTE_MISSING_STATUS:-2}"
    ;;
  tag)
    if [[ "${2:-}" == "-l" ]]; then
      grep -Fqx "${3:-}" <<< "${TEST_LOCAL_TAGS:-}" && printf '%s\n' "${3:-}"
    else
      exit 0
    fi
    ;;
  push)
    if [[ "${2:-}" == "--dry-run" && "${TEST_DRY_PUSH_FAIL:-false}" == "true" ]]; then
      exit 1
    fi
    exit 0
    ;;
esac
EOF

cat > "$FAKE_BIN/gh" <<'EOF'
#!/usr/bin/env bash
set -u
printf 'gh %s\n' "$*" >> "$TEST_LOG"

case "${1:-} ${2:-}" in
  'auth status')
    [[ "${TEST_GH_AUTH_FAIL:-false}" == "true" ]] && exit 1
    exit 0
    ;;
  'repo view')
    if [[ "${3:-}" == "${TEST_ORIGIN_PUSH_URL:-https://github.com/cvoya-com/graph}" ]]; then
      printf '%s\n' "${TEST_ORIGIN_PUSH_REPO:-cvoya-com/graph}"
    else
      printf '%s\n' "${TEST_ORIGIN_FETCH_REPO:-cvoya-com/graph}"
    fi
    ;;
  'run list')
    printf '%s\n' "${TEST_RUN_ID:-12345}"
    ;;
  'run watch')
    [[ "${TEST_RUN_FAIL:-false}" == "true" ]] && exit 1
    exit 0
    ;;
  'release view')
    printf '%s\n' "${TEST_RELEASE_ASSETS:-Cvoya.Graph.${TEST_NUGET_VERSION:-1.2.3}.nupkg}"
    ;;
  'release edit')
    ;;
esac
EOF

cat > "$FAKE_BIN/curl" <<'EOF'
#!/usr/bin/env bash
printf 'curl %s\n' "$*" >> "$TEST_LOG"
printf '{"versions":["%s"]}\n' "${TEST_NUGET_VERSION:-1.2.3}"
EOF

chmod +x "$FAKE_BIN/date" "$FAKE_BIN/sleep" "$FAKE_BIN/git" "$FAKE_BIN/gh" "$FAKE_BIN/curl"

pass=0
fail=0
LAST_OUTPUT=""
LAST_STATUS=0

reset_fakes() {
  : > "$TEST_LOG"
  export TEST_DIRTY=false TEST_DRY_PUSH_FAIL=false TEST_FETCH_FAIL=false TEST_GH_AUTH_FAIL=false
  export TEST_LOCAL_SHA=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa TEST_LOCAL_TAGS=''
  export TEST_LS_REMOTE_MISSING_STATUS=2 TEST_NUGET_VERSION=1.2.3
  export TEST_ORIGIN_FETCH_REPO=cvoya-com/graph TEST_ORIGIN_FETCH_URL=https://github.com/cvoya-com/graph
  export TEST_ORIGIN_PUSH_REPO=cvoya-com/graph TEST_ORIGIN_PUSH_URL=https://github.com/cvoya-com/graph
  export TEST_ORIGIN_SHA=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa TEST_RELEASE_ASSETS=''
  export TEST_REMOTE_TAGS='' TEST_RUN_FAIL=false TEST_RUN_ID=12345 TEST_TODAY=20260717
}

run_release() {
  set +e
  LAST_OUTPUT="$(PATH="$FAKE_BIN:$PATH" bash "$RELEASE_SCRIPT" "$@" 2>&1)"
  LAST_STATUS=$?
  set -e
}

check_status() {
  local description="$1" expected="$2"
  if [[ "$LAST_STATUS" -eq "$expected" ]]; then
    pass=$((pass + 1))
    printf 'PASS: %s\n' "$description"
  else
    fail=$((fail + 1))
    printf 'FAIL: %s (expected exit %s, got %s)\n%s\n' "$description" "$expected" "$LAST_STATUS" "$LAST_OUTPUT"
  fi
}

check_output() {
  local description="$1" expected="$2"
  if grep -Fq -- "$expected" <<< "$LAST_OUTPUT"; then
    pass=$((pass + 1))
    printf 'PASS: %s\n' "$description"
  else
    fail=$((fail + 1))
    printf 'FAIL: %s (missing output: %s)\n%s\n' "$description" "$expected" "$LAST_OUTPUT"
  fi
}

check_log() {
  local description="$1" expected="$2"
  if grep -Fq -- "$expected" "$TEST_LOG"; then
    pass=$((pass + 1))
    printf 'PASS: %s\n' "$description"
  else
    fail=$((fail + 1))
    printf 'FAIL: %s (missing command: %s)\n' "$description" "$expected"
  fi
}

check_log_absent() {
  local description="$1" unexpected="$2"
  if ! grep -Fq -- "$unexpected" "$TEST_LOG"; then
    pass=$((pass + 1))
    printf 'PASS: %s\n' "$description"
  else
    fail=$((fail + 1))
    printf 'FAIL: %s (unexpected command: %s)\n' "$description" "$unexpected"
  fi
}

reset_fakes
run_release 1.2.3 --plan
check_status "stable plan succeeds" 0
check_output "stable plan computes the stable tag" "Full version : v1.2.3"
check_log_absent "plan does not push" "git push"

reset_fakes
TEST_REMOTE_TAGS='v1.2.3-alpha.20260717'
run_release 1.2.3 --pre alpha --plan
check_status "prerelease plan succeeds" 0
check_output "prerelease plan increments an existing same-day tag" "Full version : v1.2.3-alpha.20260717.1"

reset_fakes
run_release 1.02.3 --plan
check_status "leading zeroes are rejected" 1
check_output "leading-zero rejection explains semver" "Invalid semver '1.02.3'"

reset_fakes
run_release 1.2.65535 --plan
check_status "oversized assembly-version components are rejected" 1
check_output "assembly-version limit is explained" "maximum (65534)"

reset_fakes
run_release 1.2.3 --force-retag
check_status "unsafe force-retag option is unavailable" 1
check_output "force-retag is treated as unknown" "Unknown option '--force-retag'"

reset_fakes
TEST_NUGET_VERSION='1.2.3-alpha.20260717'
TEST_RELEASE_ASSETS='Cvoya.Graph.1.2.3-alpha.20260717.nupkg'
run_release 1.2.3 --pre alpha --latest
check_status "complete prerelease flow succeeds with fakes" 0
check_log "push access is checked before release" "git push --dry-run origin aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:refs/tags/v1.2.3-alpha.20260717"
check_log "release tag is created" "git tag v1.2.3-alpha.20260717"
check_log "release workflow is watched" "gh run watch --repo cvoya-com/graph --exit-status 12345"
check_log "verified prerelease is promoted" "gh release edit v1.2.3-alpha.20260717 --repo cvoya-com/graph --latest --prerelease=false"
check_output "complete flow reports package availability" "All packages resolve on nuget.org"

reset_fakes
TEST_ORIGIN_PUSH_URL='https://github.com/example/fork'
TEST_ORIGIN_PUSH_REPO='example/fork'
run_release 1.2.3
check_status "wrong push remote is rejected" 1
check_output "wrong remote reports both endpoints" "Remote 'origin' must fetch from and push to cvoya-com/graph"
check_log_absent "wrong remote cannot create a tag" "git tag v1.2.3"

reset_fakes
TEST_DIRTY=true
run_release 1.2.3
check_status "dirty checkout is rejected" 1
check_log_absent "dirty checkout cannot create a tag" "git tag v1.2.3"

reset_fakes
TEST_LOCAL_SHA='bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
run_release 1.2.3
check_status "stale or divergent HEAD is rejected" 1
check_output "revision mismatch names origin/main" "is not the current origin/main commit"
check_log_absent "stale HEAD cannot create a tag" "git tag v1.2.3"

reset_fakes
TEST_REMOTE_TAGS='v1.2.3'
run_release 1.2.3
check_status "an existing stable tag is immutable" 1
check_output "existing tag directs workflow rerun" "Rerun its existing release workflow"
check_log_absent "existing tag is never moved" "git tag v1.2.3"

reset_fakes
TEST_GH_AUTH_FAIL=true
run_release 1.2.3
check_status "missing GitHub authentication is rejected" 1
check_log_absent "authentication failure happens before a tag" "git tag v1.2.3"

reset_fakes
TEST_RELEASE_ASSETS='Cvoya.Graph.9.9.9.nupkg'
run_release 1.2.3
check_status "mismatched release asset version is rejected" 1
check_output "asset mismatch names the required suffix" "does not end in the expected version suffix '.1.2.3.nupkg'"

printf '%s\n' '----'
printf '%s passed, %s failed\n' "$pass" "$fail"
[[ "$fail" -eq 0 ]]
