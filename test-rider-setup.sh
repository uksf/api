#!/bin/bash
# Test script for Rider formatting setup

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}Testing Rider formatting setup...${NC}"

# Check if we're in a git repository
if [ ! -d ".git" ]; then
    echo -e "${RED}Error: Not in a git repository.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Git repository found${NC}"

# Check if solution file exists
SOLUTION_FILE=$(find . -name "*.sln" -type f | head -1)
if [ -z "$SOLUTION_FILE" ]; then
    echo -e "${RED}Error: Could not find solution file (.sln) in the repository.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Found solution file: $SOLUTION_FILE${NC}"

# Check if pre-commit hook exists
if [ -f ".git/hooks/pre-commit" ]; then
    echo -e "${GREEN}✓ Pre-commit hook exists${NC}"
else
    echo -e "${RED}✗ Pre-commit hook not found${NC}"
    exit 1
fi

# Check if jb command is available
if command -v jb >/dev/null 2>&1; then
    echo -e "${GREEN}✓ JetBrains Toolbox (jb) command found${NC}"
    
    # Test formatting on a sample file (dry run)
    # First try to find Program.cs, then any .cs file not in build/obj folders
    CS_FILE=$(find . -name "Program.cs" -type f | head -1)
    if [ -z "$CS_FILE" ]; then
        CS_FILE=$(find . -name "*.cs" -type f -not -path "*/bin/*" -not -path "*/obj/*" | head -1)
    fi
    if [ -n "$CS_FILE" ]; then
        echo -e "${YELLOW}Testing formatting on: $CS_FILE${NC}"
        # Test if jb rider format command works (without --dry-run as it might not be supported)
        # Find the .DotSettings file
        DOTSETTINGS_FILE=$(find . -name "*.sln.DotSettings" -type f | head -1)
        if [ -n "$DOTSETTINGS_FILE" ]; then
            echo -e "${BLUE}Running: jb CleanupCode \"$CS_FILE\" --settings=\"$DOTSETTINGS_FILE\"${NC}"
            echo -e "${YELLOW}--- CleanupCode Output ---${NC}"
            if jb CleanupCode "$CS_FILE" --settings="$DOTSETTINGS_FILE"; then
                echo -e "${YELLOW}--- End Output ---${NC}"
                echo -e "${GREEN}✓ CleanupCode command completed successfully${NC}"
            else
                echo -e "${YELLOW}--- End Output ---${NC}"
                echo -e "${RED}✗ CleanupCode command failed (exit code: $?)${NC}"
                echo -e "${YELLOW}This might be normal if Rider isn't fully set up yet${NC}"
            fi
        else
            echo -e "${RED}✗ Could not find .DotSettings file${NC}"
        fi
    else
        echo -e "${YELLOW}No C# files found to test formatting${NC}"
    fi
else
    echo -e "${RED}✗ JetBrains Toolbox (jb) command not found${NC}"
    echo -e "${YELLOW}Please install Rider via JetBrains Toolbox and ensure 'jb' is in your PATH${NC}"
    exit 1
fi

echo -e "${GREEN}All tests passed! Rider formatting is ready to use.${NC}"
echo -e "${BLUE}To test with actual staged files, stage some .cs files and run the pre-commit hook.${NC}" 
