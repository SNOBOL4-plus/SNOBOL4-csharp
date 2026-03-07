#!/usr/bin/env bash
# snapshot.sh — zip the SNOBOL4csharp project on demand
# Usage: bash snapshot.sh [label]
# Output: SNOBOL4csharp-<label>.zip  (default label = date)

LABEL="${1:-$(date +%Y%m%d)}"
OUT="SNOBOL4csharp-${LABEL}.zip"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

cd "$SCRIPT_DIR/.." || exit 1

zip -r "$OUT" SNOBOL4csharp \
  --exclude "SNOBOL4csharp/src/SNOBOL4/bin/*" \
  --exclude "SNOBOL4csharp/src/SNOBOL4/obj/*" \
  --exclude "SNOBOL4csharp/tests/SNOBOL4.Tests/bin/*" \
  --exclude "SNOBOL4csharp/tests/SNOBOL4.Tests/obj/*"

echo "Created: $OUT"
