#!/bin/zsh
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="$ROOT_DIR/DigitalTwin.OCR/Resources/Models"
DST_DIR="$ROOT_DIR/Mobile/DigitalTwin.Mobile.OCR/Resources/Models"

echo "==> Syncing OCR models to Mobile project..."

if [[ ! -d "$SRC_DIR" ]]; then
  echo "WARNING: source models directory not found: $SRC_DIR"
  exit 0
fi

mkdir -p "$DST_DIR"

# Use rsync when available for deterministic updates and cleanup.
if command -v rsync >/dev/null 2>&1; then
  rsync -a --delete "$SRC_DIR/" "$DST_DIR/"
else
  rm -rf "$DST_DIR"
  mkdir -p "$DST_DIR"
  cp -R "$SRC_DIR/"* "$DST_DIR/" 2>/dev/null || true
fi

MODEL_COUNT=$(find "$DST_DIR" -type f | wc -l | tr -d ' ')
echo "==> Mobile OCR model sync complete ($MODEL_COUNT files)"
