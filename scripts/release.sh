#!/usr/bin/env bash
# scripts/release.sh — CVOYA graph release orchestration
#
# Publishes a coherent CVOYA graph release by pushing a single `v<version>`
# tag and watching `release.yml` to completion. Releases are tag-authoritative:
# the tag IS the version. Nothing is committed, and no workflow step ever
# stamps or rewrites a version number.
#
# Usage:
#   ./scripts/release.sh [OPTIONS] <semver>
#
# Arguments:
#   <semver>   Base semantic version, e.g. v1.0.0 or 1.0.0 (leading v optional).
#              Must match vMAJOR.MINOR.PATCH (no pre-release suffix here — use --pre).
#
# Options:
#   --pre <alpha|beta|rc>   Append a date-anchored pre-release suffix:
#                           -<suffix>.YYYYMMDD  (same-day re-runs add .1, .2, …)
#   --latest                After the workflow succeeds, promote the GitHub Release
#                           to "Latest" and clear its pre-release flag. Stable
#                           releases are already Latest, so this only matters for
#                           --pre: use it to make a chosen alpha the current
#                           default during the v1.0.0 pre-release line. The NuGet
#                           package version stays a semantic pre-release either
#                           way — this only moves the GitHub Release badge.
#   --plan                  Dry-run: print the computed tag and exit 0 without pushing.
#   -h, --help              Show this help and exit.
#
# Examples:
#   ./scripts/release.sh v1.0.0 --pre alpha     →  v1.0.0-alpha.20260716
#   ./scripts/release.sh 1.0.0  --pre rc        →  v1.0.0-rc.20260716
#   ./scripts/release.sh v1.0.0                 →  v1.0.0  (stable)
#   ./scripts/release.sh v1.0.0 --pre alpha --latest   → that alpha becomes Latest
#   ./scripts/release.sh v1.0.0 --pre alpha --plan
#
# Tag pushed:
#   v<version>  →  release.yml, which builds, tests, packs, attests, publishes to
#   nuget.org via Trusted Publishing, and creates the GitHub Release. release.yml
#   reads the version off the tag and rejects any tag that does not match the
#   scheme above.
#
# Verification:
#   After the workflow succeeds, reads the .nupkg asset names off the GitHub
#   Release and polls nuget.org until every one of those package IDs is
#   anonymously resolvable at <version>. Package IDs are read from the release
#   rather than hardcoded here, so this cannot drift from what release.yml packs.
#
# Requirements:
#   - gh CLI authenticated (`gh auth status`) with repo + workflow scopes and
#     repository administration read access for the Pages configuration check
#   - git remote `origin` pointing at cvoya-com/graph with push access
#   - curl (for the nuget.org availability check; skipped with --plan)
#   - Run from a clean checkout exactly at the current origin/main commit.

set -euo pipefail

# ── Constants ────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="cvoya-com/graph"
WORKFLOW_NAME="release.yml"

# nuget.org indexes a pushed package a short while after the API accepts it, so
# the availability check below polls rather than asking once.
NUGET_POLL_ATTEMPTS="${NUGET_POLL_ATTEMPTS:-30}"
NUGET_POLL_INTERVAL="${NUGET_POLL_INTERVAL:-20}"

print_brand_banner() {
  printf '%s\n' \
    '+------------------------------------------------------------+' \
    '| CVOYA                                                       |' \
    '| https://cvoya.com                                           |' \
    '| CVOYA graph release                                         |' \
    '+------------------------------------------------------------+'
  echo
}

print_brand_banner

# ── Parse arguments ──────────────────────────────────────────────────────────

PRE_RELEASE=""
DRY_RUN=false
MARK_LATEST=false
BASE_SEMVER=""

usage() {
  grep '^#' "$0" | grep -v '^#!/' | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --pre)
      [[ $# -lt 2 ]] && { echo "::error::--pre requires an argument (alpha|beta|rc)"; exit 1; }
      PRE_RELEASE="$2"
      if [[ ! "$PRE_RELEASE" =~ ^(alpha|beta|rc)$ ]]; then
        echo "::error::--pre value must be alpha, beta, or rc; got '$PRE_RELEASE'"
        exit 1
      fi
      shift 2
      ;;
    --latest)
      MARK_LATEST=true
      shift
      ;;
    --plan)
      DRY_RUN=true
      shift
      ;;
    -h|--help)
      usage 0
      ;;
    -v|--version)
      # The pre-tag-authoritative script took -v <semver>; point the muscle
      # memory at the positional form rather than failing as "unknown option".
      echo "::error::-v/--version is no longer accepted — pass the version positionally."
      echo "         e.g. ./scripts/release.sh 1.2.3 --pre alpha"
      exit 1
      ;;
    -*)
      echo "::error::Unknown option '$1'. Run with --help for usage."
      exit 1
      ;;
    *)
      if [[ -n "$BASE_SEMVER" ]]; then
        echo "::error::Unexpected extra argument '$1'. Run with --help for usage."
        exit 1
      fi
      BASE_SEMVER="$1"
      shift
      ;;
  esac
done

if [[ -z "$BASE_SEMVER" ]]; then
  echo "::error::Missing required <semver> argument."
  usage 1
fi

# Normalize: strip leading v and validate MAJOR.MINOR.PATCH. Leading zeroes are
# rejected because NuGet normalizes them (1.01.0 becomes 1.1.0), which would
# make the tag, package filename, and published version disagree.
BASE_SEMVER="${BASE_SEMVER#v}"
if ! [[ "$BASE_SEMVER" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "::error::Invalid semver '$BASE_SEMVER'. Expected MAJOR.MINOR.PATCH (e.g. 1.0.0)."
  echo "         Pre-release suffixes are not passed here — use --pre alpha|beta|rc."
  exit 1
fi

# ── Compute full version ──────────────────────────────────────────────────────
#
# Pre-release counter: increments the candidate `.N` suffix while the
# `v<candidate>` tag exists either on the remote or locally. Cutting two alphas
# on the same day therefore yields -alpha.20260716 then -alpha.20260716.1, which
# order correctly under SemVer pre-release precedence (numeric identifiers
# compare numerically, and a shorter identifier list sorts lower).

TODAY="$(date -u +%Y%m%d)"

# Returns 0 if `v<arg>` exists on origin. A transport/authentication failure is
# not the same thing as a missing tag and must stop the release.
remote_tag_exists() {
  local v="$1"
  local status

  if git ls-remote --exit-code --refs --tags origin "refs/tags/v${v}" >/dev/null 2>&1; then
    return 0
  else
    status=$?
  fi

  if [[ "$status" -eq 2 ]]; then
    return 1
  fi

  echo "::error::Could not query tags on origin while checking v${v}."
  exit 1
}

# Validates the external Pages settings that release.yml cannot safely discover
# until its final deployment job. Run this immediately before creating the
# immutable release tag so a stale environment policy cannot publish packages
# and a GitHub Release before rejecting the documentation deployment.
validate_pages_release_configuration() {
  local pages_build_type
  local environment_policy_mode
  local deployment_policies
  local environment_url="/repos/${REPO}/environments/github-pages"

  if ! pages_build_type="$(gh api \
      --method GET \
      "/repos/${REPO}/pages" \
      --jq '.build_type' 2>/dev/null)"; then
    echo "::error::Could not read the GitHub Pages configuration for ${REPO}."
    echo "         The authenticated account needs repository administration read access."
    return 1
  fi

  if [[ "$pages_build_type" != "workflow" ]]; then
    echo "::error::GitHub Pages must use GitHub Actions before releasing; found build type '${pages_build_type:-unset}'."
    echo "         Configure Settings > Pages > Build and deployment > Source."
    return 1
  fi

  if ! environment_policy_mode="$(gh api \
      --method GET \
      "$environment_url" \
      --jq '[.deployment_branch_policy.protected_branches, .deployment_branch_policy.custom_branch_policies] | @tsv' \
      2>/dev/null)"; then
    echo "::error::Could not read the github-pages environment configuration for ${REPO}."
    echo "         The authenticated account needs repository administration read access."
    return 1
  fi

  if [[ "$environment_policy_mode" != $'false\ttrue' ]]; then
    echo "::error::The github-pages environment must use custom deployment branch and tag policies."
    echo "         Configure Settings > Environments > github-pages > Deployment branches and tags"
    echo "         to Selected branches and tags."
    return 1
  fi

  if ! deployment_policies="$(gh api \
      --method GET \
      "${environment_url}/deployment-branch-policies?per_page=100" \
      --jq '[.branch_policies[] | "\(.type):\(.name)"] | sort | join(",")' \
      2>/dev/null)"; then
    echo "::error::Could not read the github-pages deployment policies for ${REPO}."
    echo "         The authenticated account needs repository administration read access."
    return 1
  fi

  if [[ "$deployment_policies" != "tag:v*" ]]; then
    echo "::error::The github-pages environment must allow exactly the release tag policy 'v*'."
    echo "         Expected: tag:v*"
    echo "         Found:    ${deployment_policies:-none}"
    return 1
  fi
}

# Returns 0 (true) if `v<arg>` exists either remotely or locally.
tag_exists() {
  local v="$1"
  remote_tag_exists "$v" && return 0
  git tag -l "v${v}" | grep -q .
}

if [[ -n "$PRE_RELEASE" ]]; then
  FULL_SEMVER="${BASE_SEMVER}-${PRE_RELEASE}.${TODAY}"
  CANDIDATE="${FULL_SEMVER}"
  COUNTER=1
  while tag_exists "${CANDIDATE}"; do
    CANDIDATE="${FULL_SEMVER}.${COUNTER}"
    COUNTER=$((COUNTER + 1))
  done
  FULL_SEMVER="${CANDIDATE}"
else
  FULL_SEMVER="${BASE_SEMVER}"
fi

if ! version_error="$("$SCRIPT_DIR/validate-release-version.sh" "$FULL_SEMVER" 2>&1)"; then
  echo "::error::$version_error"
  exit 1
fi

RELEASE_VERSION="v${FULL_SEMVER}"
RELEASE_TAG="${RELEASE_VERSION}"

# ── Resolve "will this release be latest?" ───────────────────────────────────
#
# Stable releases are always Latest. A pre-release becomes Latest only when the
# operator passed --latest; unlike Spring Voyage (where the intent has to reach
# a container-tagging job via an annotated tag trailer) nothing in release.yml
# needs to know, because promotion here is a single `gh release edit` this
# script performs after the workflow succeeds.
WILL_BE_LATEST=false
if [[ -z "$PRE_RELEASE" || "$MARK_LATEST" == "true" ]]; then
  WILL_BE_LATEST=true
fi
if [[ "$MARK_LATEST" == "true" && -z "$PRE_RELEASE" ]]; then
  echo "ℹ  --latest is redundant for a stable release (stable is always Latest); proceeding."
fi

# ── Dry-run / --plan mode ─────────────────────────────────────────────────────

if [[ "$DRY_RUN" == "true" ]]; then
  echo "=== Release plan (dry-run — no tags will be pushed) ==="
  echo ""
  echo "  Full version : ${RELEASE_VERSION}"
  echo "  Tag to push  : ${RELEASE_TAG}"
  if [[ -n "$PRE_RELEASE" ]]; then
    echo "  Pre-release  : yes (${PRE_RELEASE})"
  else
    echo "  Pre-release  : no (stable)"
  fi
  echo "  Marked latest: ${WILL_BE_LATEST}$( [[ "$WILL_BE_LATEST" == "true" && -n "$PRE_RELEASE" ]] && echo "  (gh release edit --latest --prerelease=false)" )"
  echo "  Workflow     : ${WORKFLOW_NAME}"
  echo ""
  echo "  Step 1  push tag  ${RELEASE_TAG}"
  echo "          wait for  ${WORKFLOW_NAME}"
  echo ""
  echo "  Step 2  verify every published package resolves on nuget.org at ${FULL_SEMVER}"
  if [[ "$WILL_BE_LATEST" == "true" && -n "$PRE_RELEASE" ]]; then
    echo ""
    echo "  Step 3  promote GitHub Release ${RELEASE_TAG} to Latest"
  fi
  exit 0
fi

# ── Destructive-operation preflight ──────────────────────────────────────────
#
# Complete every local tooling, authentication, repository, and revision check
# before pushing the tag. Once the tag is pushed, release.yml can publish
# immutable packages even if this local script exits immediately afterwards.

for required_command in git gh curl; do
  if ! command -v "$required_command" >/dev/null 2>&1; then
    echo "::error::Required command '$required_command' was not found on PATH."
    exit 1
  fi
done

if ! gh auth status --hostname github.com >/dev/null 2>&1; then
  echo "::error::GitHub CLI is not authenticated for github.com. Run 'gh auth login' and retry."
  exit 1
fi

ORIGIN_FETCH_URL="$(git remote get-url origin 2>/dev/null || true)"
ORIGIN_PUSH_URL="$(git remote get-url --push origin 2>/dev/null || true)"
if [[ -z "$ORIGIN_FETCH_URL" || -z "$ORIGIN_PUSH_URL" ]]; then
  echo "::error::Git remote 'origin' is not configured."
  exit 1
fi

ORIGIN_FETCH_REPO="$(gh repo view "$ORIGIN_FETCH_URL" --json nameWithOwner --jq .nameWithOwner 2>/dev/null || true)"
ORIGIN_PUSH_REPO="$(gh repo view "$ORIGIN_PUSH_URL" --json nameWithOwner --jq .nameWithOwner 2>/dev/null || true)"
if [[ "$ORIGIN_FETCH_REPO" != "$REPO" || "$ORIGIN_PUSH_REPO" != "$REPO" ]]; then
  echo "::error::Remote 'origin' must fetch from and push to ${REPO}."
  echo "         fetch: ${ORIGIN_FETCH_URL} (${ORIGIN_FETCH_REPO:-unresolved})"
  echo "         push:  ${ORIGIN_PUSH_URL} (${ORIGIN_PUSH_REPO:-unresolved})"
  exit 1
fi

if [[ -n "$(git status --porcelain --untracked-files=normal)" ]]; then
  echo "::error::The checkout has tracked or untracked changes. Commit, stash, or remove them before releasing."
  exit 1
fi

if ! git fetch origin main --quiet; then
  echo "::error::Could not refresh origin/main; refusing to release from potentially stale state."
  exit 1
fi

LOCAL_SHA="$(git rev-parse HEAD)"
ORIGIN_SHA="$(git rev-parse origin/main)"
if [[ "$LOCAL_SHA" != "$ORIGIN_SHA" ]]; then
  echo "::error::HEAD (${LOCAL_SHA:0:12}) is not the current origin/main commit (${ORIGIN_SHA:0:12})."
  echo "         Check out main, run 'git pull --ff-only', and retry."
  exit 1
fi

# ── Local/remote tag divergence check ────────────────────────────────────────
#
# A local tag with no matching remote tag is unexpected state — it means a
# previous run was interrupted after `git tag` but before `git push`, or the
# remote tag was deleted without cleaning up locally. Fail hard here so the
# operator can decide: delete the local tag and reuse this version, or start
# fresh with a new one.

if git tag -l "${RELEASE_TAG}" | grep -q . && ! remote_tag_exists "${FULL_SEMVER}"; then
  echo "::error::Local tag '${RELEASE_TAG}' exists but is not on origin."
  echo ""
  echo "         The remote tag was likely deleted without cleaning up locally."
  echo "         Delete it and rerun:"
  echo "           git tag -d ${RELEASE_TAG}"
  exit 1
fi

# ── Idempotency guard ─────────────────────────────────────────────────────────
#
# Re-tagging a version that already published is not recoverable on nuget.org:
# package versions are immutable and cannot be replaced, so a re-run would push
# different bits under a version that already exists. If a workflow failed,
# rerun that workflow against the existing tag instead of moving the tag.

if remote_tag_exists "${FULL_SEMVER}"; then
  echo "::error::Tag '${RELEASE_TAG}' already exists on origin."
  echo "         Rerun its existing release workflow, or choose a new version. Tags are never moved."
  exit 1
fi

if ! git push --dry-run origin "${LOCAL_SHA}:refs/tags/${RELEASE_TAG}" >/dev/null; then
  echo "::error::The release tag cannot be pushed to origin. No remote state was changed."
  exit 1
fi

if ! validate_pages_release_configuration; then
  echo "         No release tag was created or pushed."
  exit 1
fi

# ── Helper: push the tag and wait for the triggered workflow ──────────────────

push_and_wait() {
  local tag="$1"
  local workflow_name="$2"

  echo ""
  echo "▶  Pushing tag ${tag} …"
  git tag "${tag}"
  git push origin "refs/tags/${tag}"

  echo "   Waiting for workflow '${workflow_name}' to register …"
  local run_id=""
  local attempt=0
  while [[ -z "$run_id" || "$run_id" == "null" ]]; do
    attempt=$((attempt + 1))
    if (( attempt > 36 )); then
      echo "::error::Timed out waiting for workflow '${workflow_name}' to start for tag '${tag}' (3 min)"
      exit 1
    fi
    sleep 5
    run_id="$(gh run list \
      --repo "${REPO}" \
      --workflow "${workflow_name}" \
      --branch "${tag}" \
      --limit 1 \
      --json databaseId \
      --jq '.[0].databaseId' 2>/dev/null || true)"
  done

  echo "   Watching run ${run_id} …"
  if ! gh run watch \
      --repo "${REPO}" \
      --exit-status \
      "${run_id}"; then
    echo "::error::${workflow_name} failed for ${tag}. The tag remains immutable; rerun workflow ${run_id} after fixing the failure."
    exit 1
  fi

  echo "✓  ${workflow_name} succeeded."
}

# ── Main release sequence ─────────────────────────────────────────────────────

echo ""
echo "╔══════════════════════════════════════════════════════════════════╗"
echo "║  CVOYA graph release: ${RELEASE_VERSION}"
echo "╚══════════════════════════════════════════════════════════════════╝"

push_and_wait "${RELEASE_TAG}" "${WORKFLOW_NAME}"

# ── nuget.org availability verification ───────────────────────────────────────
#
# The workflow succeeding means `dotnet nuget push` was accepted, not that the
# packages are consumable: nuget.org indexes asynchronously, and a package can
# also be accepted but held by validation. Verify what a real consumer would see
# — an anonymous lookup of each ID at this exact version.
#
# The IDs come from the .nupkg assets attached to the GitHub Release, so this
# always checks exactly the set release.yml packed. A hardcoded list here would
# silently stop covering any package added to the pack job later.

echo ""
echo "▶  Verifying nuget.org availability for every published package …"

PACKAGE_IDS=()
PACKAGE_SUFFIX=".${FULL_SEMVER}.nupkg"
while IFS= read -r asset_name; do
  [[ -z "$asset_name" ]] && continue
  if [[ "$asset_name" != *"$PACKAGE_SUFFIX" ]]; then
    echo "::error::Release asset '$asset_name' does not end in the expected version suffix '$PACKAGE_SUFFIX'."
    exit 1
  fi
  PACKAGE_IDS+=("${asset_name%"$PACKAGE_SUFFIX"}")
done < <(
  gh release view "${RELEASE_TAG}" \
    --repo "${REPO}" \
    --json assets \
    --jq '.assets[].name | select(endswith(".nupkg"))' 2>/dev/null \
    | sort -u
)

if [[ ${#PACKAGE_IDS[@]} -eq 0 ]]; then
  echo "::error::No .nupkg assets found on release ${RELEASE_TAG}."
  echo "         The workflow reported success, so this is unexpected — inspect the"
  echo "         release and the pack job before announcing this version."
  exit 1
fi

# Polls the flat-container index until <id> lists <version>. nuget.org lowercases
# package IDs in these URLs.
nuget_has_version() {
  local id_lower
  local response
  id_lower="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  response="$(curl --fail --silent --max-time 30 \
    "https://api.nuget.org/v3-flatcontainer/${id_lower}/index.json" 2>/dev/null)" || return 1
  grep -Fq "\"${FULL_SEMVER}\"" <<< "$response"
}

FAILED=()
for id in "${PACKAGE_IDS[@]}"; do
  printf '   %s %s … ' "${id}" "${FULL_SEMVER}"
  attempt=0
  until nuget_has_version "${id}"; do
    attempt=$((attempt + 1))
    if (( attempt >= NUGET_POLL_ATTEMPTS )); then
      echo "✗  FAIL (not indexed after $((NUGET_POLL_ATTEMPTS * NUGET_POLL_INTERVAL / 60)) min)"
      FAILED+=("${id}")
      break
    fi
    sleep "${NUGET_POLL_INTERVAL}"
  done
  if (( attempt < NUGET_POLL_ATTEMPTS )); then
    echo "✓"
  fi
done

if [[ ${#FAILED[@]} -gt 0 ]]; then
  echo ""
  echo "::error::These packages did not become resolvable on nuget.org:"
  for f in "${FAILED[@]}"; do
    echo "         ${f} ${FULL_SEMVER}"
  done
  echo ""
  echo "         Indexing is usually minutes; check https://www.nuget.org/profiles/cvoya"
  echo "         for a package held by validation before re-running anything."
  exit 1
fi

# ── Promote to Latest ─────────────────────────────────────────────────────────
#
# release.yml marks any semver pre-release as a GitHub pre-release, which keeps
# it off `releases/latest`. During a pre-release line there is still a "current"
# build that public download links should resolve to, so --latest clears the
# pre-release flag on the GitHub Release record only. The tag and the NuGet
# package version remain semantic pre-releases.

if [[ "$MARK_LATEST" == "true" && -n "$PRE_RELEASE" ]]; then
  echo ""
  echo "▶  Promoting GitHub Release ${RELEASE_TAG} to Latest …"
  gh release edit "${RELEASE_TAG}" \
    --repo "${REPO}" \
    --latest \
    --prerelease=false
  echo "✓  ${RELEASE_TAG} is now Latest."
  echo "   Verify: https://github.com/${REPO}/releases/latest/download/cvoya-graph-source.zip"
fi

echo ""
echo "╔══════════════════════════════════════════════════════════════════╗"
echo "║  Release ${RELEASE_VERSION} complete. All packages resolve on nuget.org."
echo "╚══════════════════════════════════════════════════════════════════╝"
echo ""
