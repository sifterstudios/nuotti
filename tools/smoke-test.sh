#!/bin/bash
# Smoke test script for local development
# This script performs a quick sanity check that the backend is working
# Usage: ./tools/smoke-test.sh [--backend-url <url>] [--session <code>]

set -e

BACKEND_URL="${NUOTTI_BACKEND:-http://localhost:5240}"
SESSION="${NUOTTI_SESSION:-smoke-test}"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --backend-url)
            BACKEND_URL="$2"
            shift 2
            ;;
        --session)
            SESSION="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo "[smoke-test] Starting smoke test..."
echo "[smoke-test] Backend URL: $BACKEND_URL"
echo "[smoke-test] Session: $SESSION"

# Check if backend is already running
echo "[smoke-test] Checking if backend is running..."
if curl -f -s "$BACKEND_URL/health/live" > /dev/null; then
    echo "[smoke-test] Backend is already running"
else
    echo "[smoke-test] ERROR: Backend is not running or not accessible at $BACKEND_URL"
    echo "[smoke-test] Please start the backend first: dotnet run --project Nuotti.Backend"
    exit 1
fi

# Create session
echo "[smoke-test] Creating session..."
if curl -f -s -X POST "$BACKEND_URL/api/sessions/$SESSION" > /dev/null; then
    echo "[smoke-test] Session created successfully"
else
    echo "[smoke-test] ERROR: Failed to create session"
    exit 1
fi

# Upload manifest with one song
echo "[smoke-test] Uploading manifest..."
MANIFEST='{"songs":[{"title":"Smoke Test Song","artist":"Test Artist","file":"https://example.com/smoke-test.mp3"}]}'

if curl -f -s -X POST "$BACKEND_URL/api/manifest/$SESSION" \
    -H "Content-Type: application/json" \
    -d "$MANIFEST" > /dev/null; then
    echo "[smoke-test] Manifest uploaded successfully"
else
    echo "[smoke-test] ERROR: Failed to upload manifest"
    exit 1
fi

# Push a question
echo "[smoke-test] Pushing question..."
QUESTION='{"text":"What is the answer?","options":["Option A","Option B","Option C"],"sessionCode":"'$SESSION'","issuedByRole":2,"issuedById":"smoke-test"}'

if curl -f -s -X POST "$BACKEND_URL/api/pushQuestion/$SESSION" \
    -H "Content-Type: application/json" \
    -d "$QUESTION" > /dev/null; then
    echo "[smoke-test] Question pushed successfully"
else
    echo "[smoke-test] ERROR: Failed to push question"
    exit 1
fi

# Verify session status
echo "[smoke-test] Verifying session status..."
if STATUS=$(curl -f -s "$BACKEND_URL/status/$SESSION"); then
    PHASE=$(echo "$STATUS" | grep -o '"phase":"[^"]*"' | cut -d'"' -f4 || echo "unknown")
    echo "[smoke-test] Session status verified: Phase = $PHASE"
else
    echo "[smoke-test] ERROR: Failed to get session status"
    exit 1
fi

echo "[smoke-test] SMOKE PASS"
exit 0

