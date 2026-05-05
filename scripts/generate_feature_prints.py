#!/usr/bin/env python3
"""
generate_feature_prints.py — Step D (optional visual fallback).

Runs VNGenerateImageFeaturePrintRequest on each reference photo and serialises
the feature vectors to Mobile/DigitalTwin.Mobile.OCR/Resources/Models/reference_feature_prints.json.

These vectors are loaded at runtime by FeaturePrintDocumentClassifier.cs for
nearest-neighbour visual classification (cosine distance). No model training required.

Usage:
  python scripts/generate_feature_prints.py

Requirements:
  pip install pyobjc-framework-Vision pyobjc-framework-Quartz

Runs on macOS only.
"""

import json
import pathlib
import struct
import sys

REFERENCE_DIR  = pathlib.Path("models/reference_photos")
OUTPUT_FILE    = pathlib.Path("Mobile/DigitalTwin.Mobile.OCR/Resources/Models/reference_feature_prints.json")

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

def label_from_path(image_path: pathlib.Path) -> str:
    # models/reference_photos/<Label>/<file>
    try:
        return image_path.parent.name
    except Exception:
        return ""


def compute_feature_print(image_path: pathlib.Path) -> list[float]:
    """Compute Vision feature print vector for an image as float list.

    NOTE: On some macOS/iOS versions Vision returns VNSceneObservation (rev 1) instead
    of VNFeaturePrintObservation (rev 2). Both expose:
      - elementCount()
      - data()  -> NSData containing float32 values
    """
    try:
        import Vision
        import Quartz
    except ImportError:
        print("ERROR: pyobjc bindings not found. Install: pip install pyobjc-framework-Vision pyobjc-framework-Quartz")
        sys.exit(1)

    url = Quartz.CFURLCreateWithFileSystemPath(
        None, str(image_path.resolve()), Quartz.kCFURLPOSIXPathStyle, False)
    image_source = Quartz.CGImageSourceCreateWithURL(url, None)
    if image_source is None:
        raise ValueError(f"Cannot load: {image_path}")
    cg_image = Quartz.CGImageSourceCreateImageAtIndex(image_source, 0, None)
    if cg_image is None:
        raise ValueError(f"Cannot decode: {image_path}")

    results: list = []

    def handler(request, error):
        if error:
            return
        for obs in request.results():
            n = int(obs.elementCount())
            data = obs.data()
            # NSData -> Python bytes
            try:
                blob = bytes(data)
            except TypeError:
                # Fallback: use memoryview
                blob = memoryview(data).tobytes()

            # Some Vision versions include padding; trust elementCount.
            needed = n * 4
            if len(blob) < needed:
                raise RuntimeError(f"Feature print data too small: {len(blob)} bytes, need {needed}")

            vec = list(struct.unpack(f"<{n}f", blob[:needed]))
            results.append(vec)

    request = Vision.VNGenerateImageFeaturePrintRequest.alloc().initWithCompletionHandler_(handler)
    # Prefer revision 2 (iOS 17+ / macOS 14+, 768-float) — falls back to revision 1 automatically
    try:
        request.setRevision_(2)
    except Exception:
        pass

    req_handler = Vision.VNImageRequestHandler.alloc().initWithCGImage_options_(cg_image, {})
    ok, err = req_handler.performRequests_error_([request], None)
    if not ok or not results:
        raise RuntimeError(f"Feature print failed for {image_path}: {err}")

    return results[0]


def main():
    if not REFERENCE_DIR.exists():
        print(f"ERROR: Missing directory: {REFERENCE_DIR}/")
        sys.exit(1)

    # Only consider images under class folders; ignore root-level images and _ignore folders.
    class_dirs = sorted(
        p for p in REFERENCE_DIR.iterdir()
        if p.is_dir() and not p.name.startswith(".") and not p.name.startswith("_")
    )
    images: list[pathlib.Path] = []
    for d in class_dirs:
        if d.name not in ALLOWED_LABELS:
            print(f"ERROR: Unknown class folder '{d.name}'.")
            print(f"Allowed labels: {', '.join(sorted(ALLOWED_LABELS))}")
            sys.exit(1)
        images.extend(
            p for p in d.iterdir()
            if p.is_file() and p.suffix.lower() in {".jpg", ".jpeg", ".png", ".heic"}
        )
    images = sorted(images)

    if not images:
        print(f"No images found in {REFERENCE_DIR}/")
        sys.exit(1)

    print(f"Computing feature prints for {len(images)} image(s)…")
    entries = []
    for img in images:
        label = label_from_path(img)
        print(f"  [{label}] {img.name} ...", end=" ", flush=True)
        try:
            vector = compute_feature_print(img)
            entries.append({"label": label, "source_image": str(img.relative_to(REFERENCE_DIR)), "vector": vector})
            print(f"OK (dim={len(vector)})")
        except Exception as exc:
            print(f"FAILED: {exc}")

    OUTPUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_FILE.open("w", encoding="utf-8") as f:
        json.dump(entries, f, separators=(",", ":"))

    print(f"\nFeature prints saved → {OUTPUT_FILE}  ({len(entries)} entries)")
    print("This file will be bundled in the app as a BundleResource.")


if __name__ == "__main__":
    main()
