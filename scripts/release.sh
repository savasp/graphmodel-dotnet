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
#   --force-retag           Skip the idempotency guard (re-tag an existing version).
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
#   - gh CLI authenticated (`gh auth status`) with repo + workflow scopes
#   - git remote `origin` pointing at cvoya-com/graph with push access
#   - curl (for the nuget.org availability check; skipped with --plan)
#   - Run from a clean checkout of main.

set -euo pipefail

# ── Constants ────────────────────────────────────────────────────────────────

REPO="cvoya-com/graph"
WORKFLOW_NAME="release.yml"

# nuget.org indexes a pushed package a short while after the API accepts it, so
# the availability check below polls rather than asking once.
NUGET_POLL_ATTEMPTS=30
NUGET_POLL_INTERVAL=20

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
FORCE_RETAG=false
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
    --force-retag)
      FORCE_RETAG=true
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

# Normalize: strip leading v, validate MAJOR.MINOR.PATCH
BASE_SEMVER="${BASE_SEMVER#v}"
if ! [[ "$BASE_SEMVER" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
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

# Returns 0 (true) if `v<arg>` exists either as a remote tag on origin or as a
# local tag in the current worktree.
tag_exists() {
  local v="$1"
  git ls-remote --exit-code --tags origin "v${v}" >/dev/null 2>&1 && return 0
  git tag -l "v${v}" | grep -q .
}

if [[ -n "$PRE_RELEASE" ]]; then
  FULL_SEMVER="${BASE_SEMVER}-${PRE_RELEASE}.${TODAY}"
  if [[ "$FORCE_RETAG" != "true" ]]; then
    CANDIDATE="${FULL_SEMVER}"
    COUNTER=1
    while tag_exists "${CANDIDATE}"; do
      CANDIDATE="${FULL_SEMVER}.${COUNTER}"
      COUNTER=$((COUNTER + 1))
    done
    FULL_SEMVER="${CANDIDATE}"
  fi
else
  FULL_SEMVER="${BASE_SEMVER}"
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

# ── HEAD must be on origin/main ──────────────────────────────────────────────
#
# Tags point to HEAD. If HEAD is not reachable from origin/main, releasing would
# tag commits that aren't in the canonical history — and `git push origin <tag>`
# would ship those unreviewed commits to the remote alongside the tag, bypassing
# branch protection on main. Fail fast here.

git fetch origin main --quiet 2>/dev/null || true
LOCAL_SHA="$(git rev-parse HEAD)"
ORIGIN_SHA="$(git rev-parse origin/main 2>/dev/null || true)"
if [[ -z "$ORIGIN_SHA" ]] || ! git merge-base --is-ancestor "${LOCAL_SHA}" origin/main 2>/dev/null; then
  echo "::error::HEAD (${LOCAL_SHA:0:12}) is not on origin/main."
  echo "         Push your commits to origin/main (via PR) before releasing."
  exit 1
fi

# ── Local main behind origin/main? ───────────────────────────────────────────
#
# The ancestor check above PASSES when HEAD is merely BEHIND origin/main (HEAD is
# still reachable from it). Tagging then cuts the release from a stale commit,
# silently omitting changes already merged to origin/main — e.g. a fix merged via
# PR but not pulled locally. Surface the gap and let the operator choose: sync to
# origin/main, or deliberately release the current commit.

BEHIND_COUNT="$(git rev-list --count "${LOCAL_SHA}..origin/main" 2>/dev/null || echo 0)"
if [[ "${BEHIND_COUNT}" -gt 0 ]]; then
  echo ""
  echo "⚠  Local HEAD is ${BEHIND_COUNT} commit(s) behind origin/main."
  echo "     HEAD         ${LOCAL_SHA:0:12}  $(git log -1 --format=%s "${LOCAL_SHA}" 2>/dev/null)"
  echo "     origin/main  ${ORIGIN_SHA:0:12}  $(git log -1 --format=%s origin/main 2>/dev/null)"
  echo ""
  echo "   Releasing now tags ${LOCAL_SHA:0:12} and OMITS these commits already on origin/main:"
  git log --oneline "${LOCAL_SHA}..origin/main" 2>/dev/null | sed 's/^/       /'
  echo ""

  if [[ -t 0 ]] || [[ -r /dev/tty ]]; then
    REPLY_SYNC=""
    printf '   (s)ync to origin/main and release that, (p)roceed on current HEAD, or (a)bort? [s/p/a] '
    if [[ -t 0 ]]; then
      IFS= read -r REPLY_SYNC || REPLY_SYNC=""
    else
      IFS= read -r REPLY_SYNC </dev/tty || REPLY_SYNC=""
    fi
    case "${REPLY_SYNC}" in
      [Ss]*)
        echo "   Fast-forwarding local main to origin/main …"
        if ! git merge --ff-only origin/main; then
          echo "::error::Could not fast-forward to origin/main (uncommitted changes, or not on main?)."
          echo "         Resolve with 'git pull --ff-only', then rerun."
          exit 1
        fi
        LOCAL_SHA="$(git rev-parse HEAD)"
        echo "   Synced — now at ${LOCAL_SHA:0:12}; releasing from origin/main."
        ;;
      [Pp]*)
        echo "   Proceeding on current HEAD (${LOCAL_SHA:0:12}); the commits above will NOT be in this release."
        ;;
      *)
        echo "   Aborted. Run 'git pull --ff-only' to sync, then rerun (or choose 'p' to release this commit deliberately)."
        exit 1
        ;;
    esac
  else
    echo "::error::Local main is behind origin/main and there is no terminal to confirm intent."
    echo "         Sync with 'git pull --ff-only origin main' and rerun, or run interactively to choose."
    exit 1
  fi
fi

# ── Local/remote tag divergence check ────────────────────────────────────────
#
# A local tag with no matching remote tag is unexpected state — it means a
# previous run was interrupted after `git tag` but before `git push`, or the
# remote tag was deleted without cleaning up locally. Fail hard here so the
# operator can decide: delete the local tag and reuse this version, or start
# fresh with a new one.

if git tag -l "${RELEASE_TAG}" | grep -q . &&
   ! git ls-remote --exit-code --tags origin "${RELEASE_TAG}" >/dev/null 2>&1; then
  echo "::error::Local tag '${RELEASE_TAG}' exists but is not on origin."
  echo ""
  echo "         The remote tag was likely deleted without cleaning up locally."
  echo "         Delete it and rerun:"
  echo "           git tag -d ${RELEASE_TAG}"
  exit 1
fi

# ── Idempotency guard (stable releases only — pre-release handled above) ──────
#
# Re-tagging a version that already published is not recoverable on nuget.org:
# package versions are immutable and cannot be replaced, so a re-run would push
# different bits under a version that already exists (--skip-duplicate makes the
# workflow pass while silently publishing nothing).

if [[ -z "$PRE_RELEASE" && "$FORCE_RETAG" != "true" ]]; then
  if git ls-remote --exit-code --tags origin "${RELEASE_TAG}" >/dev/null 2>&1; then
    echo "::error::Tag '${RELEASE_TAG}' already exists on origin."
    echo "         Use --force-retag to override (this will re-trigger the workflow)."
    exit 1
  fi
fi

# ── Helper: push the tag and wait for the triggered workflow ──────────────────

push_and_wait() {
  local tag="$1"
  local workflow_name="$2"

  echo ""
  echo "▶  Pushing tag ${tag} …"
  git tag "${tag}"
  git push origin "${tag}"

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
  gh run watch \
    --repo "${REPO}" \
    --exit-status \
    "${run_id}"

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

mapfile -t PACKAGE_IDS < <(
  gh release view "${RELEASE_TAG}" \
    --repo "${REPO}" \
    --json assets \
    --jq '.assets[].name | select(endswith(".nupkg"))' 2>/dev/null \
    | sed "s/\.${FULL_SEMVER}\.nupkg\$//" \
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
  id_lower="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  curl --fail --silent --max-time 30 \
    "https://api.nuget.org/v3-flatcontainer/${id_lower}/index.json" 2>/dev/null \
    | grep -Fq "\"${FULL_SEMVER}\""
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
