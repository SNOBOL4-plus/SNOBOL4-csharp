#!/usr/bin/env bash
# snapshot.sh — zip the SNOBOL4cs project on demand
# Usage: bash snapshot.sh [label]
# Output: SNOBOL4cs-<label>.zip  (default label = date)

LABEL="${1:-$(date +%Y%m%d)}"
OUT="SNOBOL4cs-${LABEL}.zip"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

cd "$SCRIPT_DIR/.." || exit 1

zip -r "$OUT" SNOBOL4cs \
  --exclude "SNOBOL4cs/src/SNOBOL4/bin/*" \
  --exclude "SNOBOL4cs/src/SNOBOL4/obj/*" \
  --exclude "SNOBOL4cs/tests/SNOBOL4.Tests/bin/*" \
  --exclude "SNOBOL4cs/tests/SNOBOL4.Tests/obj/*"

echo "Created: $OUT"
