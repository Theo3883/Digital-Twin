#!/usr/bin/env python3
"""
extract_ocr.py — Step A of the training data pipeline.

Runs Apple Vision OCR on every image in models/reference_photos/ and saves
the raw OCR text to models/raw_ocr/<ClassName>/<filename>.txt.

The class label is determined by the directory name:
  models/reference_photos/<ClassName>/<image>.<ext>

Usage:
  python scripts/extract_ocr.py

Requirements:
  pip install pyobjc-framework-Vision pyobjc-framework-Quartz

Runs on macOS only (uses Apple Vision framework).
"""

import os
import sys
import pathlib

REFERENCE_DIR = pathlib.Path("models/reference_photos")
OUTPUT_DIR    = pathlib.Path("models/raw_ocr")

OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

ALLOWED_LABELS: set[str] = {
    "Prescription",
    "Referral",
    "LabResult",
    "Discharge",
    "MedicalCertificate",
    "ImagingReport",
    "EcgReport",
    "OperativeReport",
    "ConsultationNote",
    "GenericClinicForm",
}


def run_vision_ocr(image_path: pathlib.Path) -> str:
    """Use Apple Vision VNRecognizeTextRequest to extract text from an image."""
    try:
        import Vision
        import Quartz
    except ImportError:
        print("ERROR: pyobjc Vision/Quartz bindings not found.")
        print("Install with:  pip install pyobjc-framework-Vision pyobjc-framework-Quartz")
        sys.exit(1)

    url = Quartz.CFURLCreateWithFileSystemPath(
        None, str(image_path.resolve()), Quartz.kCFURLPOSIXPathStyle, False)

    image_source = Quartz.CGImageSourceCreateWithURL(url, None)
    if image_source is None:
        raise ValueError(f"Could not load image: {image_path}")

    cg_image = Quartz.CGImageSourceCreateImageAtIndex(image_source, 0, None)
    if cg_image is None:
        raise ValueError(f"Could not decode image: {image_path}")

    results: list[str] = []

    def completion_handler(request, error):
        if error:
            return
        for observation in request.results():
            candidates = observation.topCandidates_(1)
            if candidates:
                results.append(candidates[0].string())

    request = Vision.VNRecognizeTextRequest.alloc().initWithCompletionHandler_(completion_handler)
    request.setRecognitionLevel_(Vision.VNRequestTextRecognitionLevelAccurate)
    request.setRecognitionLanguages_(["ro-RO", "en-US"])
    request.setUsesLanguageCorrection_(True)

    handler = Vision.VNImageRequestHandler.alloc().initWithCGImage_options_(cg_image, {})
    success, err = handler.performRequests_error_([request], None)
    if not success:
        raise RuntimeError(f"Vision OCR failed: {err}")

    return "\n".join(results)


def main():
    if not REFERENCE_DIR.exists():
        print(f"ERROR: Missing directory: {REFERENCE_DIR}/")
        sys.exit(1)

    class_dirs = sorted(
        p for p in REFERENCE_DIR.iterdir()
        if p.is_dir() and not p.name.startswith(".") and not p.name.startswith("_")
    )
    if not class_dirs:
        print(f"No class folders found in {REFERENCE_DIR}/")
        print("Expected structure: models/reference_photos/<ClassName>/<image>.(jpg|png|heic)")
        sys.exit(1)

    # If user has images directly in reference_photos/, warn (old convention created bad labels like 'Gemini').
    root_images = sorted(
        p for p in REFERENCE_DIR.iterdir()
        if p.is_file() and p.suffix.lower() in {".jpg", ".jpeg", ".png", ".heic"}
    )
    if root_images:
        print("WARNING: Found images directly under models/reference_photos/.")
        print("         These are ignored to prevent accidental labels (e.g. 'Gemini_*').")
        for img in root_images[:10]:
            print(f"  - ignored: {img.name}")
        if len(root_images) > 10:
            print(f"  ... and {len(root_images) - 10} more")

    total_images = 0
    for d in class_dirs:
        images = [p for p in d.iterdir() if p.is_file() and p.suffix.lower() in {".jpg", ".jpeg", ".png", ".heic"}]
        total_images += len(images)

    if total_images == 0:
        print(f"No images found under {REFERENCE_DIR}/<ClassName>/")
        sys.exit(1)

    print(f"Found {total_images} reference photo(s) across {len(class_dirs)} class folder(s).")

    for class_dir in class_dirs:
        label = class_dir.name
        if label not in ALLOWED_LABELS:
            print(f"ERROR: Unknown class folder '{label}'.")
            print(f"Allowed labels: {', '.join(sorted(ALLOWED_LABELS))}")
            sys.exit(1)

        images = sorted(
            p for p in class_dir.iterdir()
            if p.is_file() and p.suffix.lower() in {".jpg", ".jpeg", ".png", ".heic"}
        )
        if not images:
            continue

        out_dir = OUTPUT_DIR / label
        out_dir.mkdir(parents=True, exist_ok=True)

        for img in images:
            out_path = out_dir / f"{img.stem}.txt"

            print(f"  [{label}] {img.name} → raw_ocr/{label}/{out_path.name} ...", end=" ", flush=True)
            try:
                text = run_vision_ocr(img)
                # Prepend label as first line so augment.py can read it easily
                out_path.write_text(f"__label__: {label}\n{text}", encoding="utf-8")
                print(f"OK ({len(text)} chars)")
            except Exception as exc:
                print(f"FAILED: {exc}")

    print(f"\nRaw OCR text saved to {OUTPUT_DIR}/")
    print("Next step: python scripts/augment.py")


if __name__ == "__main__":
    main()
