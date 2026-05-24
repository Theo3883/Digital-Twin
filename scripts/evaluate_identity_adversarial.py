#!/usr/bin/env python3
"""Evaluate the identity validator against an adversarial case set.

Reads a JSON dataset with cases shaped like:
 {
   "patient_profile": {"name": "...", "cnp": "..."},
   "cases": [
     {"id": "A01", "doc_name": "...", "doc_cnp": "...", "expected": "ACCEPT"}
   ],
   "name_examples": [
     {"doc_name": "Theodor Sandu"},
     {"doc_name": "Țeodor Șandu"},
     {"doc_name": "SANDU Theodor"},
     {"doc_name": "Theo Sandu"}
   ]
 }

Each case is turned into a small OCR text snippet and passed directly through
NativeAOT via mobile_engine_validate_identity.

Outputs:
 - results/table3_adversarial.csv
 - results/table3_adversarial.md
 - results/table3_adversarial_compact.md
 - results/table3_adversarial_summary.json
 - results/table_names_compact.md

Console output includes an ASCII Table 3.
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import sqlite3
import sys
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any

try:
    from nativehost_bridge import NativeHostBridge
except Exception:
    NativeHostBridge = None

DEFAULT_NAME_EXAMPLES: tuple[dict[str, str], ...] = (
    {"doc_name": "Theodor Sandu"},
    {"doc_name": "Țeodor Șandu"},
    {"doc_name": "SANDU Theodor"},
    {"doc_name": "Theo Sandu"},
)

@dataclass
class CaseResult:
    case_id: str
    category: str
    expected: str
    actual: str
    passed: bool
    doc_name: str
    doc_cnp: str
    reason: str
    name_matched: bool | None = None
    cnp_matched: bool | None = None
    is_valid: bool | None = None

@dataclass
class NameExampleResult:
    doc_name: str
    normalized: str
    actual: str
    reason: str
    edit_distance: int
    sorted_match: bool

def _build_ocr_text(doc_name: str, doc_cnp: str, category: str) -> str:
    parts: list[str] = []
    if doc_name:
        parts.append(f"Nume: {doc_name}")
    if doc_cnp:
        parts.append(f"CNP: {doc_cnp}")
    return "\n".join(parts)

def _normalize_expected(value: str) -> str:
    return "ACCEPT" if str(value).strip().upper() == "ACCEPT" else "REJECT"

def _seed_identity_profile_sqlite(db_path: Path, patient_profile: dict[str, Any]) -> None:
    """Seed a current user and patient row so the validator can resolve a profile."""
    user_id = "11111111-1111-1111-1111-111111111111"
    patient_id = "22222222-2222-2222-2222-222222222222"
    profile_name = str(patient_profile.get("name", "") or "").strip()
    name_parts = profile_name.split()
    first_name = name_parts[0] if name_parts else "Theodor"
    last_name = " ".join(name_parts[1:]) if len(name_parts) > 1 else "Sandu"
    cnp = str(patient_profile.get("cnp", "") or "").strip()

    conn = sqlite3.connect(str(db_path))
    try:
        conn.execute(
            """
            INSERT OR REPLACE INTO Users
            (Id, Email, Role, FirstName, LastName, PhotoUrl, Phone, Address, City, Country, DateOfBirth, CreatedAt, UpdatedAt, IsSynced)
            VALUES
            (?, ?, ?, ?, ?, NULL, NULL, NULL, NULL, NULL, NULL, ?, ?, 1)
            """,
            (
                user_id,
                "theodor.sandu@example.com",
                0,
                first_name,
                last_name,
                "2026-01-01T00:00:00Z",
                "2026-01-01T00:00:00Z",
            ),
        )
        conn.execute(
            """
            INSERT OR REPLACE INTO Patients
            (Id, UserId, BloodType, Allergies, MedicalHistoryNotes, Weight, Height,
             BloodPressureSystolic, BloodPressureDiastolic, Cholesterol, Cnp, CreatedAt, UpdatedAt, IsSynced)
            VALUES
            (?, ?, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, ?, ?, ?, 1)
            """,
            (
                patient_id,
                user_id,
                cnp,
                "2026-01-01T00:00:00Z",
                "2026-01-01T00:00:00Z",
            ),
        )
        conn.commit()
    finally:
        conn.close()

def _actual_from_result(result: dict[str, Any]) -> tuple[str, bool | None, bool | None, bool | None, str]:
    if not isinstance(result, dict):
        return "REJECT", None, None, None, "Invalid result payload"

    raw_valid = result.get("isValid")
    if raw_valid is None:
        raw_valid = result.get("IsValid")
    is_valid = bool(raw_valid) if raw_valid is not None else None

    raw_name = result.get("nameMatched")
    if raw_name is None:
        raw_name = result.get("NameMatched")
    name_matched = bool(raw_name) if raw_name is not None else None

    raw_cnp = result.get("cnpMatched")
    if raw_cnp is None:
        raw_cnp = result.get("CnpMatched")
    cnp_matched = bool(raw_cnp) if raw_cnp is not None else None

    reason = result.get("reason")
    if reason is None:
        reason = result.get("Reason")
    reason_text = str(reason or "")

    actual = "ACCEPT" if is_valid else "REJECT"
    return actual, name_matched, cnp_matched, is_valid, reason_text

def _normalize_name(value: str) -> str:
    text = unicodedata.normalize("NFKD", str(value or ""))
    text = "".join(ch for ch in text if not unicodedata.combining(ch))
    text = text.lower()
    text = re.sub(r"[^a-z0-9]+", " ", text)
    return " ".join(text.split())

def _sorted_normalized_name(value: str) -> str:
    return " ".join(sorted(_normalize_name(value).split()))

def _levenshtein(a: str, b: str) -> int:
    if a == b:
        return 0
    if not a:
        return len(b)
    if not b:
        return len(a)

    previous = list(range(len(b) + 1))
    for i, ch_a in enumerate(a, start=1):
        current = [i]
        for j, ch_b in enumerate(b, start=1):
            cost = 0 if ch_a == ch_b else 1
            current.append(
                min(
                    previous[j] + 1,
                    current[j - 1] + 1,
                    previous[j - 1] + cost,
                )
            )
        previous = current
    return previous[-1]

def _latex_escape(value: str) -> str:
    replacements = {
        "\\": r"\textbackslash{}",
        "&": r"\&",
        "%": r"\%",
        "$": r"\$",
        "#": r"\#",
        "_": r"\_",
        "{": r"\{",
        "}": r"\}",
        "~": r"\textasciitilde{}",
        "^": r"\textasciicircum{}",
    }
    return "".join(replacements.get(ch, ch) for ch in str(value))

def _print_table(rows: list[CaseResult]) -> None:
    print("\n══════════════════ Table 3 — Adversarial identity validator set ══════════════════")
    print(f"{'ID':<6}{'Category':<28}{'Expected':<10}{'Actual':<10}{'Pass':<8}{'Reason'}")
    print("─" * 110)
    for row in rows:
        print(
            f"{row.case_id:<6}{row.category[:27]:<28}{row.expected:<10}{row.actual:<10}"
            f"{('YES' if row.passed else 'NO'):<8}{row.reason}"
        )

def _load_name_examples(dataset: dict[str, Any]) -> list[dict[str, str]]:
    raw_examples = dataset.get("name_examples")
    if isinstance(raw_examples, list) and raw_examples:
        examples: list[dict[str, str]] = []
        for item in raw_examples:
            if isinstance(item, dict):
                doc_name = str(item.get("doc_name", "") or "").strip()
                if not doc_name:
                    continue
                examples.append(
                    {
                        "doc_name": doc_name,
                        "doc_cnp": str(item.get("doc_cnp", "") or "").strip(),
                        "note": str(item.get("note", "") or "").strip(),
                    }
                )
            else:
                doc_name = str(item or "").strip()
                if doc_name:
                    examples.append({"doc_name": doc_name, "doc_cnp": "", "note": ""})
        if examples:
            return examples

    return [dict(example) for example in DEFAULT_NAME_EXAMPLES]

def _evaluate_name_examples(
    bridge: Any,
    patient_profile: dict[str, Any],
    dataset: dict[str, Any],
) -> list[NameExampleResult]:
    profile_name = str(patient_profile.get("name", "") or "").strip() or "Theodor Sandu"
    profile_cnp = str(patient_profile.get("cnp", "") or "").strip()
    profile_normalized = _normalize_name(profile_name)
    profile_sorted = _sorted_normalized_name(profile_name)

    results: list[NameExampleResult] = []
    for example in _load_name_examples(dataset):
        doc_name = str(example.get("doc_name", "") or "").strip()
        doc_cnp = str(example.get("doc_cnp", "") or "").strip() or profile_cnp
        ocr_text = _build_ocr_text(doc_name, doc_cnp, "name_example")
        payload = bridge.validate_identity(ocr_text)
        actual, _, _, _, reason = _actual_from_result(payload)

        normalized = _normalize_name(doc_name)
        edit_distance = _levenshtein(normalized, profile_normalized)
        sorted_match = normalized != profile_normalized and _sorted_normalized_name(doc_name) == profile_sorted

        if not reason:
            reason = str(example.get("note", "") or "").strip()

        results.append(
            NameExampleResult(
                doc_name=doc_name,
                normalized=normalized,
                actual=actual,
                reason=reason,
                edit_distance=edit_distance,
                sorted_match=sorted_match,
            )
        )

    return results

def _format_name_example_outcome(row: NameExampleResult) -> str:
    outcome = f"\\textsc{{{row.actual.lower()}}}"
    if row.sorted_match:
        return outcome + "; sorted"
    if row.actual == "REJECT":
        return outcome + f" ($d={row.edit_distance}$)"
    if row.reason:
        return outcome + "; " + _latex_escape(row.reason)
    return outcome

def _write_name_examples_table(path: Path, rows: list[NameExampleResult]) -> None:
    with path.open("w", encoding="utf-8") as fh:
        fh.write("\\begin{table}[t]\n")
        fh.write("\\caption{Name normalisation and matching examples.}\n")
        fh.write("\\label{tab:names}\n")
        fh.write("\\footnotesize\n")
        fh.write("\\setlength{\\tabcolsep}{3pt}\n")
        fh.write("\\begin{tabular}{@{}L{0.31\\columnwidth}L{0.34\\columnwidth}L{0.25\\columnwidth}@{}}\n")
        fh.write("\\toprule\n")
        fh.write("\\textbf{Document name} & \\textbf{Normalised} & \\textbf{Outcome} \\\\\n")
        fh.write("\\midrule\n")
        for row in rows:
            fh.write(
                f"{_latex_escape(row.doc_name)} & \\texttt{{{_latex_escape(row.normalized)}}} & {_format_name_example_outcome(row)} \\\\\n"
            )
        fh.write("\\bottomrule\n")
        fh.write("\\end{tabular}\n")
        fh.write("\\end{table}\n")

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--dataset",
        type=Path,
        default=Path("data/identity_adversarial_set.json"),
        help="Path to identity_adversarial_set.json",
    )
    parser.add_argument(
        "--native-host",
        type=Path,
        required=True,
        help="Path to DigitalTwin.Mobile.NativeHost.dylib",
    )
    parser.add_argument(
        "--out-dir",
        type=Path,
        default=Path("results"),
        help="Directory for output artifacts",
    )
    args = parser.parse_args()

    args.out_dir.mkdir(parents=True, exist_ok=True)
    dataset = json.loads(args.dataset.read_text(encoding="utf-8"))
    patient_profile = dataset.get("patient_profile", {})
    cases = dataset.get("cases", [])

    if NativeHostBridge is None:
        raise RuntimeError("nativehost_bridge.py could not be imported")

    bridge = NativeHostBridge(str(args.native_host))
    bridge.initialize(str((args.out_dir / "identity_native_db").resolve()), "", "", "", "", "", "")
    bridge.initialize_database()

    db_path = args.out_dir / "identity_native_db"
    _seed_identity_profile_sqlite(db_path, patient_profile)

    results: list[CaseResult] = []
    for case in cases:
        case_id = str(case.get("id", ""))
        category = str(case.get("category", ""))
        expected = _normalize_expected(case.get("expected", "REJECT"))
        doc_name = str(case.get("doc_name", "") or "")
        doc_cnp = str(case.get("doc_cnp", "") or "")

        ocr_text = _build_ocr_text(doc_name, doc_cnp, category)
        payload = bridge.validate_identity(ocr_text)
        actual, name_matched, cnp_matched, is_valid, reason = _actual_from_result(payload)
        passed = actual == expected

        if not reason:
            if is_valid is True:
                reason = "Validator accepted the document"
            elif is_valid is False:
                reason = "Validator rejected the document"
            else:
                reason = "No structured reason returned"

        results.append(
            CaseResult(
                case_id=case_id,
                category=category,
                expected=expected,
                actual=actual,
                passed=passed,
                doc_name=doc_name,
                doc_cnp=doc_cnp,
                reason=reason,
                name_matched=name_matched,
                cnp_matched=cnp_matched,
                is_valid=is_valid,
            )
        )

    total = len(results)
    correct = sum(1 for r in results if r.passed)
    wrong = total - correct
    accuracy = correct / total if total else 0.0

    _print_table(results)
    print(f"\nSummary: {correct}/{total} correct, {wrong} wrong, accuracy={accuracy:.3f}")

    csv_path = args.out_dir / "table3_adversarial.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(
            fh,
            fieldnames=[
                "id",
                "category",
                "doc_name",
                "doc_cnp",
                "expected",
                "actual",
                "passed",
                "name_matched",
                "cnp_matched",
                "is_valid",
                "reason",
            ],
        )
        writer.writeheader()
        for row in results:
            writer.writerow(
                {
                    "id": row.case_id,
                    "category": row.category,
                    "doc_name": row.doc_name,
                    "doc_cnp": row.doc_cnp,
                    "expected": row.expected,
                    "actual": row.actual,
                    "passed": str(row.passed),
                    "name_matched": "" if row.name_matched is None else str(row.name_matched),
                    "cnp_matched": "" if row.cnp_matched is None else str(row.cnp_matched),
                    "is_valid": "" if row.is_valid is None else str(row.is_valid),
                    "reason": row.reason,
                }
            )

    md_path = args.out_dir / "table3_adversarial.md"
    with md_path.open("w", encoding="utf-8") as fh:
        fh.write("# Table 3 - Adversarial identity validator set\n\n")
        fh.write("| ID | Category | Expected | Actual | Pass | Reason |\n")
        fh.write("| --- | --- | --- | --- | --- | --- |\n")
        for row in results:
            fh.write(
                f"| {row.case_id} | {row.category} | {row.expected} | {row.actual} | "
                f"{('YES' if row.passed else 'NO')} | {row.reason.replace('|', '/')} |\n"
            )
        fh.write(f"\nAccuracy: {accuracy:.3f} ({correct}/{total})\n")
        fh.write(f"\nPatient profile: {patient_profile.get('name', '')} / {patient_profile.get('cnp', '')}\n")

    compact_path = args.out_dir / "table3_adversarial_compact.md"
    with compact_path.open("w", encoding="utf-8") as fh:
        fh.write("\\begin{table}[t]\n")
        fh.write("\\caption{Adversarial identity validator set.}\n")
        fh.write("\\label{tab:identity-adversarial}\n")
        fh.write("\\footnotesize\n")
        fh.write("\\setlength{\\tabcolsep}{3pt}\n")
        fh.write("\\begin{tabular}{@{}L{0.26\\columnwidth}L{0.22\\columnwidth}L{0.40\\columnwidth}@{}}\n")
        fh.write("\\toprule\n")
        fh.write("\\textbf{Case ID} & \\textbf{Expected} & \\textbf{Outcome} \\\\\n")
        fh.write("\\midrule\n")
        for row in results:
            expected = row.expected.lower()
            actual = row.actual.lower()
            outcome = f"\\textsc{{{actual}}}"
            if not row.passed:
                outcome += " (mismatch)"
            elif expected != actual:
                outcome += " (normalized)"
            fh.write(f"{row.case_id} & \\textsc{{{expected}}} & {outcome} \\\\\n")
        fh.write("\\bottomrule\n")
        fh.write("\\end{tabular}\n")
        fh.write("\\end{table}\n\n")
        fh.write(f"Accuracy: {accuracy:.3f} ({correct}/{total})\n")

    summary_path = args.out_dir / "table3_adversarial_summary.json"
    summary_path.write_text(
        json.dumps(
            {
                "total": total,
                "correct": correct,
                "wrong": wrong,
                "accuracy": accuracy,
                "patient_profile": patient_profile,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    name_results = _evaluate_name_examples(bridge, patient_profile, dataset)
    names_path = args.out_dir / "table_names_compact.md"
    _write_name_examples_table(names_path, name_results)

    print(f"Name examples table written to {names_path.resolve()}")
    print(f"\nArtifacts written to {args.out_dir.resolve()}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
