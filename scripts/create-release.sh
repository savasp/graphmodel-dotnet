#!/bin/bash

# GraphModel Release Version Creator
# Creates a new release version and optionally builds packages

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 GraphModel Release Creator${NC}"
echo ""

# Read existing VERSION file to get default semantic version
DEFAULT_SEMVER=""
if [ -f VERSION ]; then
    PREV_VERSION=$(cat VERSION | head -n1)
    # Remove the first .YYYYMMDD.rev and everything after it
    DEFAULT_SEMVER=$(echo "$PREV_VERSION" | sed -E 's/\.[0-9]{8}\.[0-9]+.*$//')
fi

# Prompt for semantic version (e.g., 1.0.0-alpha)
if [ -n "$DEFAULT_SEMVER" ]; then
    echo -e "${YELLOW}📝 Enter semantic version (e.g., 1.0.0-alpha) [default: $DEFAULT_SEMVER]:${NC}"
    read -r SEMVER
    if [ -z "$SEMVER" ]; then
        SEMVER="$DEFAULT_SEMVER"
    fi
else
    echo -e "${YELLOW}📝 Enter semantic version (e.g., 1.0.0-alpha):${NC}"
    read -r SEMVER
fi

# Always strip any .YYYYMMDD.rev from SEMVER in case user pasted a full version string
SEMVER=$(echo "$SEMVER" | grep -oE '^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?')

if [ -z "$SEMVER" ]; then
    echo -e "${RED}❌ Semantic version cannot be empty${NC}"
    exit 1
fi

# Validate semantic version (basic check)
if ! [[ $SEMVER =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]]; then
    echo -e "${RED}❌ Invalid semantic version format. Example: 1.0.0-alpha${NC}"
    exit 1
fi

# Check if prerelease section exists (e.g., -alpha, -beta)
if [[ $SEMVER =~ -[a-zA-Z0-9.]+$ ]]; then
    # Prerelease: add date and revision
    TODAY=$(date +%Y%m%d)
    PREV_VERSION=""
    PREV_DATE=""
    PREV_REV=""
    if [ -f VERSION ]; then
        PREV_VERSION=$(cat VERSION | head -n1)
        # Extract date and revision from previous version (assumes format: x.y.z(-qualifier)?.YYYYMMDD.rev)
        if [[ $PREV_VERSION =~ ([0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?)\.([0-9]{8})\.([0-9]+) ]]; then
            PREV_DATE="${BASH_REMATCH[3]}"
            PREV_REV="${BASH_REMATCH[4]}"
        fi
    fi
    if [ "$PREV_DATE" == "$TODAY" ]; then
        REVISION=$((PREV_REV + 1))
    else
        REVISION=0
    fi
    FULL_VERSION="${SEMVER}.${TODAY}.${REVISION}"
else
    # Stable: use as-is
    FULL_VERSION="$SEMVER"
fi

echo -e "${BLUE}🎯 Creating release version: $FULL_VERSION${NC}"
echo "$FULL_VERSION" > VERSION
echo -e "${GREEN}✅ VERSION file updated${NC}"

# Update Directory.Build.props to use the version from VERSION file
TEMP_FILE=$(mktemp)
awk -v version="$FULL_VERSION" -v full_version="$FULL_VERSION" '
    /<AssemblyVersion>/ {
        print "        <AssemblyVersion>" version "</AssemblyVersion>"
        next
    }
    /<FileVersion>/ {
        print "        <FileVersion>" version "</FileVersion>"
        next
    }
    /<Version>/ {
        print "        <Version>" full_version "</Version>"
        next
    }
    /<PackageVersion>/ {
        print "        <PackageVersion>" full_version "</PackageVersion>"
        next
    }
    { print }
' Directory.Build.props > "$TEMP_FILE"
mv "$TEMP_FILE" Directory.Build.props
echo -e "${GREEN}✅ Directory.Build.props updated${NC}"

# Handle VERSION.ASSEMBLY
IFS='.' read -r MAJOR MINOR PATCH <<< "${SEMVER%%-*}"
ASSEMBLY_BASE="$MAJOR.$MINOR"
# Get UTC year and day of year
YEAR=$(date -u +%Y)
YEAR_SHORT=$((YEAR - 2000))
DAY_OF_YEAR=$(date -u +%j)
ASSEMBLY_BUILD=$((YEAR_SHORT * 1000 + 10#$DAY_OF_YEAR))
# Get UTC hour and minute
ASSEMBLY_REVISION=$(date -u +%H%M)

ASSEMBLY_VERSION="$ASSEMBLY_BASE.$ASSEMBLY_BUILD.$ASSEMBLY_REVISION"
echo "$ASSEMBLY_VERSION" > VERSION.ASSEMBLY
echo -e "${GREEN}✅ VERSION.ASSEMBLY file updated: $ASSEMBLY_VERSION${NC}"

# (Preserve the rest of the script: build, commit, output, etc.)

# Output version details and next steps

# For output: set the quality qualifier string
if [[ $SEMVER =~ -[a-zA-Z0-9.]+$ ]]; then
    QUALIFIER="${SEMVER#*-}"
    QUALIFIER_MSG="   • Quality qualifier: $QUALIFIER"
else
    QUALIFIER_MSG="   • Quality qualifier: (stable release)"
fi

echo ""
echo -e "${GREEN}🎉 Release $FULL_VERSION created successfully!${NC}"
echo ""
echo -e "${BLUE}📋 Version details:${NC}"
echo "   • Version number: $SEMVER"
echo "$QUALIFIER_MSG"
echo "   • Full package version: $FULL_VERSION"
echo ""
echo -e "${BLUE}📋 Next steps:${NC}"
if [ "$BUILD_LOCAL" != "true" ] && [ "$BUILD_RELEASE" != "true" ]; then
    echo "   • Build release:    dotnet build --configuration Release -p:UsePackageReferences=true"
fi
if [ "$COMMIT" != "true" ]; then
    echo "   • Commit version:   git add VERSION Directory.Build.props && git commit -m 'Release $FULL_VERSION'"
fi
echo "   • Test packages:    dotnet build --configuration Release"
echo "   • Publish packages: dotnet nuget push artifacts/*.nupkg" 