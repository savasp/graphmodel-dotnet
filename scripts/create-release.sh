#!/usr/bin/env bash

# GraphModel Release Version Creator
#
# Updates the VERSION file (and VERSION.ASSEMBLY) to a new release version.
# Directory.Build.props reads both files at build time, so no other file
# needs to be touched — VERSION is the single source of truth for the
# package version.
#
# Usage:
#   scripts/create-release.sh -v <semver> [--commit] [--dry-run]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

usage() {
  cat <<EOF
Usage: $(basename "$0") -v <semver> [options]

Options:
  -v, --version <semver>   Semantic version to release (e.g. 1.2.3, 1.2.3-alpha)
  --commit                 Commit the updated VERSION/VERSION.ASSEMBLY files
  --dry-run                Print what would happen without writing any files
  -h, --help                Show this help

Examples:
  $(basename "$0") -v 1.2.3
  $(basename "$0") -v 1.2.3-alpha --commit
  $(basename "$0") -v 1.2.3-rc.1 --dry-run
EOF
}

SEMVER=""
COMMIT=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    -v|--version)
      SEMVER="${2:-}"
      shift 2
      ;;
    --commit)
      COMMIT=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo -e "${RED}Unknown argument: $1${NC}" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$SEMVER" ]]; then
  echo -e "${RED}A version is required. Pass -v/--version <semver>.${NC}" >&2
  usage >&2
  exit 1
fi

if ! [[ $SEMVER =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]]; then
  echo -e "${RED}Invalid semantic version format. Example: 1.2.3 or 1.2.3-alpha${NC}" >&2
  exit 1
fi

echo -e "${BLUE}GraphModel Release Creator${NC}"
echo ""
echo -e "${BLUE}Version to release:${NC} $SEMVER"

# Derive an AssemblyVersion/FileVersion-compatible stamp (Major.Minor.YYDDD.HHMM).
# AssemblyVersion/FileVersion must be purely numeric four-part versions, so this
# is tracked separately from the semver package version in VERSION.
IFS='.' read -r MAJOR MINOR _ <<< "${SEMVER%%-*}"
ASSEMBLY_BASE="$MAJOR.$MINOR"
YEAR_SHORT=$(( $(date -u +%Y) - 2000 ))
DAY_OF_YEAR=$(date -u +%j)
ASSEMBLY_BUILD=$((YEAR_SHORT * 1000 + 10#$DAY_OF_YEAR))
ASSEMBLY_REVISION=$(date -u +%H%M)
ASSEMBLY_VERSION="$ASSEMBLY_BASE.$ASSEMBLY_BUILD.$ASSEMBLY_REVISION"

echo -e "${BLUE}Derived assembly version:${NC} $ASSEMBLY_VERSION"

if [[ "$DRY_RUN" == "true" ]]; then
  echo ""
  echo -e "${YELLOW}Dry run — no files written.${NC}"
  echo "Would write:"
  echo "  VERSION          -> $SEMVER"
  echo "  VERSION.ASSEMBLY -> $ASSEMBLY_VERSION"
  if [[ "$COMMIT" == "true" ]]; then
    echo "Would commit VERSION and VERSION.ASSEMBLY with message: 'chore: release $SEMVER'"
  fi
  exit 0
fi

echo "$SEMVER" > "$REPO_ROOT/VERSION"
echo -e "${GREEN}VERSION file updated${NC}"

echo "$ASSEMBLY_VERSION" > "$REPO_ROOT/VERSION.ASSEMBLY"
echo -e "${GREEN}VERSION.ASSEMBLY file updated${NC}"

if [[ "$COMMIT" == "true" ]]; then
  git -C "$REPO_ROOT" add VERSION VERSION.ASSEMBLY
  git -C "$REPO_ROOT" commit -m "chore: release $SEMVER"
  echo -e "${GREEN}Committed VERSION and VERSION.ASSEMBLY${NC}"
fi

echo ""
echo -e "${GREEN}Release $SEMVER prepared successfully!${NC}"
echo ""
echo -e "${BLUE}Next steps:${NC}"
if [[ "$COMMIT" != "true" ]]; then
  echo "  1. Commit:  git add VERSION VERSION.ASSEMBLY && git commit -m 'chore: release $SEMVER'"
  echo "  2. Tag:     git tag v$SEMVER && git push origin v$SEMVER"
else
  echo "  1. Tag:     git tag v$SEMVER && git push origin v$SEMVER"
fi
echo "  Pushing the tag triggers .github/workflows/release.yml, which builds,"
echo "  tests, packs, publishes to NuGet, and creates the GitHub Release."
