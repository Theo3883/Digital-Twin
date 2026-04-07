#!/usr/bin/env bash
# train.sh — Full training pipeline for the on-device document type classifier.
#
# Prerequisites:
#   - macOS 13+ with Xcode 15+
#   - Python 3.11+  with: pip install python-dotenv requests pyobjc-framework-Vision pyobjc-framework-Quartz
#   - .env file with OPENROUTER_API_KEY=<your key>   (or use --skip-synthetic)
#   - Reference photos in models/reference_photos/<ClassName>/
#
# Steps:
#   1. Extract OCR text from reference photos
#   2. EDA augmentation of raw OCR text
#   3. Synthetic data generation via OpenRouter API (or offline templates)
#   4. Corpus validity check (>= 20 examples per class)
#   5. Train Create ML NL Text Classifier (transfer learning)
#   6. Compile .mlmodel → .mlmodelc
#   7. Generate Vision feature prints for visual fallback
#   8. Update manifest.json with SHA-256 checksums
#
# Usage:
#   bash scripts/train.sh [--skip-ocr] [--skip-synthetic]

set -euo pipefail

# Always execute from repo root, regardless of where train.sh is launched from.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

SKIP_OCR=false
SKIP_SYNTHETIC=false

for arg in "$@"; do
  case $arg in
    --skip-ocr)        SKIP_OCR=true ;;
    --skip-synthetic)  SKIP_SYNTHETIC=true ;;
  esac
done

echo "==================================================================="
echo "  DigitalTwin ML Training Pipeline"
echo "==================================================================="
echo ""

# ── Step 1: OCR extraction ────────────────────────────────────────────
if [ "$SKIP_OCR" = false ]; then
  echo "[1/8] Extracting OCR text from reference photos…"
  python3 "$ROOT_DIR/scripts/extract_ocr.py"
  echo ""
else
  echo "[1/8] SKIP: OCR extraction (--skip-ocr)"
fi

# ── Step 2: EDA augmentation ─────────────────────────────────────────
echo "[2/8] Augmenting training data (EDA)…"
python3 "$ROOT_DIR/scripts/augment.py" --variants 12
echo ""

# ── Step 3: Synthetic data generation ────────────────────────────────
if [ "$SKIP_SYNTHETIC" = false ]; then
  echo "[3/8] Generating synthetic training data via OpenRouter…"
  python3 "$ROOT_DIR/scripts/generate_synthetic.py" --provider openrouter
  echo ""
else
  echo "[3/8] SKIP: Synthetic generation (--skip-synthetic)"
fi

# ── Step 4: Corpus check ──────────────────────────────────────────────
echo "[4/8] Validating corpus (min 20 examples per class)…"
python3 "$ROOT_DIR/scripts/check_corpus.py" --min-per-class 20
echo ""

# ── Step 5: Train NL Text Classifier ─────────────────────────────────
echo "[5/8] Training Create ML NL Text Classifier…"
mkdir -p models/output

# Create ML training: prefer createmltools CLI if available, otherwise use Swift + CreateML framework.
if ! command -v xcrun &> /dev/null; then
  echo "ERROR: xcrun not found. Install Xcode Command Line Tools."
  exit 1
fi

if xcrun --find createmltools >/dev/null 2>&1; then
  xcrun createmltools train \
    --task text-classification \
    --data models/training_data/ \
    --output models/output/doc_type_classifier_v1.mlmodel \
    --algorithm transferLearning \
    --language ro
else
  echo "[5/8] createmltools not found — using Swift CreateML trainer…"
  xcrun swift "$ROOT_DIR/scripts/train_text_classifier.swift" \
    "$ROOT_DIR/models/training_data" \
    "$ROOT_DIR/models/output/doc_type_classifier_v1.mlmodel"
fi

echo "Training complete: models/output/doc_type_classifier_v1.mlmodel"
echo ""

# ── Step 6: Compile .mlmodel → .mlmodelc ─────────────────────────────
echo "[6/8] Compiling to binary .mlmodelc…"
RESOURCES_DIR="DigitalTwin.OCR/Resources/Models"
mkdir -p "$RESOURCES_DIR"

# Remove old compiled model if present
rm -rf "$RESOURCES_DIR/doc_type_classifier_v1.mlmodelc"

xcrun coremlcompiler compile \
  models/output/doc_type_classifier_v1.mlmodel \
  "$RESOURCES_DIR/"

echo "Compiled model: $RESOURCES_DIR/doc_type_classifier_v1.mlmodelc"
echo ""

# ── Step 7: Feature prints ────────────────────────────────────────────
echo "[7/8] Generating Vision feature prints for visual fallback…"
python3 "$ROOT_DIR/scripts/generate_feature_prints.py"
echo ""

# ── Step 8: Update manifest ───────────────────────────────────────────
echo "[8/8] Updating models/manifest.json…"
python3 "$ROOT_DIR/scripts/update_manifest.py"
echo ""

echo "==================================================================="
echo "  Training pipeline complete!"
echo ""
echo "  Artifacts ready for commit:"
echo "    DigitalTwin.OCR/Resources/Models/doc_type_classifier_v1.mlmodelc/"
echo "    DigitalTwin.OCR/Resources/Models/reference_feature_prints.json"
echo "    models/manifest.json"
echo ""
echo "  models/output/ is gitignored (temp build artefacts)."
echo ""
echo "  Commit with:"
echo "    git add DigitalTwin.OCR/Resources/Models/ models/manifest.json"
echo "    git commit -m 'chore: update ML model artifacts v1'"
echo "==================================================================="
