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

# Parse command line arguments
VERSION=""
BUILD_LOCAL=""
BUILD_RELEASE=""
COMMIT=""

while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        --build-local)
            BUILD_LOCAL="true"
            shift
            ;;
        --build-release)
            BUILD_RELEASE="true"
            shift
            ;;
        --commit)
            COMMIT="true"
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -v, --version VERSION    Specify version (e.g., 1.2.3 or 1.2.3-alpha)"
            echo "  --build-local           Build Release configuration after creating version"
            echo "  --build-release         Build Release configuration after creating version"
            echo "  --commit                Commit VERSION file to git"
            echo "  -h, --help              Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0 -v 1.2.3                          # Create stable release"
            echo "  $0 -v 1.2.3-alpha                    # Create pre-release"
            echo "  $0 -v 1.2.3 --build-local            # Create and build release"
            echo "  $0 -v 1.2.3 --build-release --commit # Create, build, and commit"
            exit 0
            ;;
        *)
            echo -e "${RED}❌ Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Prompt for version if not provided
if [ -z "$VERSION" ]; then
    echo -e "${YELLOW}📝 Enter release version (e.g., 1.2.3 or 1.2.3-alpha):${NC}"
    read -r VERSION
fi

# Validate version format
if [ -z "$VERSION" ]; then
    echo -e "${RED}❌ Version cannot be empty${NC}"
    exit 1
fi

# Create the release version
echo -e "${BLUE}🎯 Creating release version: $VERSION${NC}"
dotnet msbuild -target:CreateRelease -p:ReleaseVersion="$VERSION"

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Failed to create release version${NC}"
    exit 1
fi

echo ""

# Build Release if requested
if [ "$BUILD_LOCAL" = "true" ]; then
    echo -e "${BLUE}🔨 Building Release configuration...${NC}"
    dotnet build --configuration Release
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Release build complete${NC}"
    else
        echo -e "${RED}❌ Release build failed${NC}"
        exit 1
    fi
    echo ""
fi

# Build Release if requested
if [ "$BUILD_RELEASE" = "true" ]; then
    echo -e "${BLUE}🔨 Building Release configuration...${NC}"
    dotnet build --configuration Release
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Release build complete${NC}"
    else
        echo -e "${RED}❌ Release build failed${NC}"
        exit 1
    fi
    echo ""
fi

# Commit VERSION file if requested
if [ "$COMMIT" = "true" ]; then
    if command -v git &> /dev/null && [ -d .git ]; then
        echo -e "${BLUE}📝 Committing VERSION file...${NC}"
        git add VERSION
        git commit -m "Release $VERSION"
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✅ VERSION file committed${NC}"
        else
            echo -e "${YELLOW}⚠️  Git commit failed or no changes to commit${NC}"
        fi
    else
        echo -e "${YELLOW}⚠️  Git not available or not in a git repository${NC}"
    fi
fi

echo ""
echo -e "${GREEN}🎉 Release $VERSION created successfully!${NC}"
echo ""
echo -e "${BLUE}📋 Next steps:${NC}"
if [ "$BUILD_LOCAL" != "true" ] && [ "$BUILD_RELEASE" != "true" ]; then
    echo "   • Build release:    dotnet build --configuration Release"
fi
if [ "$COMMIT" != "true" ]; then
    echo "   • Commit version:   git add VERSION && git commit -m 'Release $VERSION'"
fi
echo "   • Test packages:    dotnet build --configuration Release"
echo "   • Publish packages: dotnet nuget push artifacts/*.nupkg" 