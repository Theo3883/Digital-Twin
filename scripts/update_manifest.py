#!/usr/bin/env python3
"""
update_manifest.py — Writes models/manifest.json with SHA-256 checksums
for every artifact bundled in the app under Mobile/DigitalTwin.Mobile.OCR/Resources/Models/.

The MSBuild integrity check target compares these checksums at build time.

Usage:
  python scripts/update_manifest.py
"""

import datetime
import hashlib
import json
import pathlib
import sys

ARTIFACTS_DIR = pathlib.Path("Mobile/DigitalTwin.Mobile.OCR/Resources/Models")
MANIFEST_FILE = pathlib.Path("models/manifest.json")


def sha256_file(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def sha256_dir(directory: pathlib.Path) -> str:
    """Compute a combined SHA-256 for all files in a directory (sorted by path)."""
    h = hashlib.sha256()
    for path in sorted(directory.rglob("*")):
        if path.is_file():
            rel = str(path.relative_to(directory))
            h.update(rel.encode())
            h.update(sha256_file(path).encode())
    return h.hexdigest()


def main():
    if not ARTIFACTS_DIR.exists():
        print(f"ERROR: Artifacts directory not found: {ARTIFACTS_DIR}")
        print("Run train.sh first to produce the compiled .mlmodelc.")
        sys.exit(1)

    artifacts = {}

    for item in sorted(ARTIFACTS_DIR.iterdir()):
        if item.is_dir():
            # Compiled .mlmodelc is a directory
            artifacts[item.name] = {
                "type": "directory",
                "sha256": sha256_dir(item),
            }
        elif item.is_file():
            artifacts[item.name] = {
                "type": "file",
                "size_bytes": item.stat().st_size,
                "sha256": sha256_file(item),
            }

    manifest = {
        "version": 1,
        "generated_at": datetime.datetime.utcnow().isoformat() + "Z",
        "artifacts": artifacts,
    }

    MANIFEST_FILE.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_FILE.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(f"Manifest written to {MANIFEST_FILE}")
    for name, info in artifacts.items():
        print(f"  {name}: {info['sha256'][:12]}…")


if __name__ == "__main__":
    main()
