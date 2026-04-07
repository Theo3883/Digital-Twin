#!/usr/bin/env python3
"""
check_corpus.py — Validates the training corpus before training begins.

Fails with a non-zero exit code if any class has fewer than --min-per-class examples.

Usage:
  python scripts/check_corpus.py [--min-per-class 20]
"""

import argparse
import pathlib
import sys

TRAINING_DIR = pathlib.Path("models/training_data")


def main():
    parser = argparse.ArgumentParser(description="Validate training corpus size.")
    parser.add_argument("--min-per-class", type=int, default=20,
                        help="Minimum number of text files per class (default: 20)")
    args = parser.parse_args()

    if not TRAINING_DIR.exists():
        print(f"ERROR: Training directory not found: {TRAINING_DIR}")
        sys.exit(1)

    class_dirs = [d for d in TRAINING_DIR.iterdir() if d.is_dir()]
    if not class_dirs:
        print(f"ERROR: No class subdirectories found in {TRAINING_DIR}/")
        sys.exit(1)

    print(f"Corpus report (minimum required: {args.min_per_class} per class)")
    print(f"{'Class':<25} {'Count':>7} {'Status'}")
    print("-" * 45)

    all_ok = True
    total = 0
    for class_dir in sorted(class_dirs):
        txt_files = list(class_dir.glob("*.txt"))
        count = len(txt_files)
        total += count
        ok = count >= args.min_per_class
        status = "OK" if ok else f"FAIL (need {args.min_per_class - count} more)"
        if not ok:
            all_ok = False
        print(f"{class_dir.name:<25} {count:>7}  {status}")

    print("-" * 45)
    print(f"{'TOTAL':<25} {total:>7}")

    if not all_ok:
        print("\nERROR: Some classes are below the minimum threshold.")
        print("Run generate_synthetic.py or add more augmented examples.")
        sys.exit(1)

    print(f"\nAll classes have >= {args.min_per_class} examples. Corpus is ready for training.")


if __name__ == "__main__":
    main()
