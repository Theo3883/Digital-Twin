#!/usr/bin/env python3
"""
augment.py — Step B of the training data pipeline.

Applies EDA (Easy Data Augmentation) to each raw OCR text file in models/raw_ocr/
and writes 10–15 augmented variants per file to models/training_data/<ClassName>/.

Operations applied (tuned for Romanian medical documents):
  1. Random Deletion  — removes body words (never the header line) with p=0.10
  2. Random Swap      — swaps adjacent words in body text with p=0.10
  3. OCR Noise        — substitutes Romanian diacritics at p=0.05 per char
                        ș→s  ă→a  ț→t  î→i  ê→e  (and uppercase variants)
  4. Header Variation — title line case / spacing permutations (all variants)

Usage:
  python scripts/augment.py [--variants N]  (default: 12)
"""

import argparse
import pathlib
import random
import re
import shutil

RAW_DIR      = pathlib.Path("models/raw_ocr")
TRAINING_DIR = pathlib.Path("models/training_data")

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

# Romanian diacritic OCR noise substitutions (character → noisy variant)
OCR_NOISE_MAP: dict[str, str] = {
    "ș": "s", "Ș": "S",
    "ț": "t", "Ț": "T",
    "ă": "a", "Ă": "A",
    "â": "a", "Â": "A",
    "î": "i", "Î": "I",
    "ê": "e",
}

HEADER_CASE_VARIANTS = [
    lambda s: s.upper(),
    lambda s: s.capitalize(),
    lambda s: s.title(),
    lambda s: s.lower().capitalize(),
    lambda s: re.sub(r"\s+", "  ", s),  # double-space
]


def inject_ocr_noise(text: str, p: float = 0.05) -> str:
    out = []
    for ch in text:
        if ch in OCR_NOISE_MAP and random.random() < p:
            out.append(OCR_NOISE_MAP[ch])
        else:
            out.append(ch)
    return "".join(out)


def random_deletion(words: list[str], p: float = 0.10) -> list[str]:
    if len(words) <= 3:
        return words
    return [w for w in words if random.random() > p]


def random_swap(words: list[str], p: float = 0.10) -> list[str]:
    words = words[:]
    for i in range(len(words) - 1):
        if random.random() < p:
            words[i], words[i + 1] = words[i + 1], words[i]
    return words


def augment_one(header: str, body_lines: list[str], variant_idx: int) -> str:
    """Produce one augmented variant from header + body lines."""
    rng_state = random.getstate()
    random.seed(variant_idx * 31337 + hash(header) % 999983)

    # Header: apply a case/spacing variant
    header_variant = HEADER_CASE_VARIANTS[variant_idx % len(HEADER_CASE_VARIANTS)](header)

    augmented_lines = [header_variant]
    for line in body_lines:
        words = line.split()
        if not words:
            augmented_lines.append(line)
            continue

        # Apply operations with some probability
        if variant_idx % 3 == 0:
            words = random_deletion(words, p=0.10)
        if variant_idx % 4 == 1:
            words = random_swap(words, p=0.10)

        line_out = " ".join(words)

        # OCR noise on body text
        if variant_idx % 2 == 0:
            line_out = inject_ocr_noise(line_out, p=0.05)

        augmented_lines.append(line_out)

    random.setstate(rng_state)
    return "\n".join(augmented_lines)


def process_file(raw_file: pathlib.Path, n_variants: int):
    content = raw_file.read_text(encoding="utf-8").strip()
    lines = content.split("\n")

    # First line is "__label__: ClassName"
    if not lines or not lines[0].startswith("__label__:"):
        print(f"  SKIP (no __label__ header): {raw_file.name}")
        return

    label = lines[0].split(":", 1)[1].strip()
    if label not in ALLOWED_LABELS:
        raise SystemExit(
            f"ERROR: Unknown label '{label}' in raw file {raw_file}.\n"
            f"Allowed labels: {', '.join(sorted(ALLOWED_LABELS))}\n"
            "Fix your models/reference_photos/<ClassName>/ folder names and re-run extract_ocr.py."
        )
    text_lines = lines[1:]

    # Split header (first non-empty line) from body
    header = next((l for l in text_lines if l.strip()), "")
    body   = [l for l in text_lines if l != header]

    out_dir = TRAINING_DIR / label
    out_dir.mkdir(parents=True, exist_ok=True)

    # Always copy the real OCR text first (variant 0 = clean original)
    original_out = out_dir / f"real_{raw_file.stem}.txt"
    original_out.write_text("\n".join(text_lines), encoding="utf-8")

    # Write augmented variants
    for i in range(1, n_variants + 1):
        variant_text = augment_one(header, body, i)
        out_path = out_dir / f"aug_{raw_file.stem}_v{i:02d}.txt"
        out_path.write_text(variant_text, encoding="utf-8")

    print(f"  [{label}] {raw_file.name}: 1 real + {n_variants} augmented → {out_dir}/")


def main():
    parser = argparse.ArgumentParser(description="EDA augmentation for OCR training data.")
    parser.add_argument("--variants", type=int, default=12,
                        help="Number of augmented variants per source file (default: 12)")
    args = parser.parse_args()

    raw_files = sorted(RAW_DIR.rglob("*.txt"))
    if not raw_files:
        print(f"No .txt files found in {RAW_DIR}/")
        print("Run extract_ocr.py first.")
        raise SystemExit(1)

    print(f"Augmenting {len(raw_files)} raw OCR file(s) with {args.variants} variants each…")
    for f in raw_files:
        process_file(f, args.variants)

    print(f"\nTraining data written to {TRAINING_DIR}/")
    print("Next step: python scripts/generate_synthetic.py")


if __name__ == "__main__":
    main()
