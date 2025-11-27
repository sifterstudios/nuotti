#!/bin/bash
# Pre-commit hook script for Nuotti
# Run this before committing to check formatting and linting

set -e

echo "Running pre-commit checks..."

ERRORS=0

# Check C# formatting
echo ""
echo "Checking C# code formatting..."
if dotnet format --verify-no-changes --verbosity quiet; then
    echo "C# formatting OK"
else
    echo "C# formatting errors found. Run 'dotnet format' to fix."
    ERRORS=$((ERRORS + 1))
fi

# Check frontend linting (if Node.js is available)
if command -v node &> /dev/null; then
    echo ""
    echo "Checking frontend code..."
    
    cd web || exit 1
    
    if [ -d "node_modules" ]; then
        if npx eslint . --ext .js,.ts,.svelte --max-warnings 0; then
            echo "ESLint OK"
        else
            echo "ESLint errors found. Run 'npx eslint . --fix' to fix."
            ERRORS=$((ERRORS + 1))
        fi
        
        if npx prettier --check .; then
            echo "Prettier OK"
        else
            echo "Prettier formatting errors found. Run 'npx prettier --write .' to fix."
            ERRORS=$((ERRORS + 1))
        fi
    else
        echo "Skipping frontend checks (node_modules not found)"
    fi
    
    cd ..
else
    echo ""
    echo "Skipping frontend checks (Node.js not found)"
fi

if [ $ERRORS -gt 0 ]; then
    echo ""
    echo "Pre-commit checks failed. Please fix the errors above."
    exit 1
else
    echo ""
    echo "All pre-commit checks passed!"
    exit 0
fi

