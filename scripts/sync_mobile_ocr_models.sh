#!/usr/bin/env zsh
# OCR ML artifacts now live only under Mobile/DigitalTwin.Mobile.OCR/Resources/Models/.
# scripts/train.sh writes compiled models and reference_feature_prints.json there directly.
# This script remains as a no-op compatibility shim for old CI/docs that invoked it.

set -euo pipefail
echo "==> sync_mobile_ocr_models.sh: nothing to do (canonical path is Mobile/DigitalTwin.Mobile.OCR/Resources/Models/)."
exit 0
