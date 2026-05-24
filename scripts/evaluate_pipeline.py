#!/usr/bin/env python3
"""
evaluate_pipeline.py
====================
Pipeline evaluator pentru lucrarea ta — calculează Tabelul 2 (Precizie, Recall, F1)
per câmp, pe baza extracției heuristice (+ clasificare CoreML), comparat cu
ground_truth_all.json.

Folosește exact componentele existente:
  1. OCR  -> macOS Vision (PyObjC) -- același engine ca iOS pipeline.
            Fallback: pytesseract (ron+eng).
  2. CLF  -> CoreML doc_type_classifier_v1.mlmodelc (coremltools).
            Fallback: regex keyword classifier din DocumentTypeClassifier.cs.
  3. EXT  -> regex heuristic identic cu OcrTextProcessingServices.cs
            (HeuristicFieldExtractor + MedicalHistoryExtractor + lab values).

Usage
-----
    python3 evaluate_pipeline.py \
        --data-dir   data \
        --gt         data/ground_truth_all.json \
        --model      data/MLModels/doc_type_classifier_v1.mlmodelc \
        --ocr-cache  data/ocr_cache \
        --out-dir    results
"""
from __future__ import annotations
import argparse, csv, json, os, re, sys, unicodedata, uuid, time
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from pathlib import Path as _Path
try:
    from nativehost_bridge import NativeHostBridge
except Exception:
    NativeHostBridge = None

# ─────────────────────────────────────────────────────────────────────────────
#  1. OCR — macOS Vision (PyObjC) cu fallback la pytesseract
# ─────────────────────────────────────────────────────────────────────────────
def _ocr_vision(image_path: Path) -> str:
    """OCR via Apple Vision (același engine ca iOS). Necesită macOS + pyobjc."""
    import Vision, Quartz                                  # pyobjc-framework-Vision
    from Foundation import NSURL
    url = NSURL.fileURLWithPath_(str(image_path))
    src = Quartz.CGImageSourceCreateWithURL(url, None)
    if src is None:
        raise RuntimeError(f"Cannot open image {image_path}")
    cg = Quartz.CGImageSourceCreateImageAtIndex(src, 0, None)
    handler = Vision.VNImageRequestHandler.alloc().initWithCGImage_options_(cg, None)
    req = Vision.VNRecognizeTextRequest.alloc().init()
    req.setRecognitionLevel_(Vision.VNRequestTextRecognitionLevelAccurate)
    req.setUsesLanguageCorrection_(True)
    try:
        req.setRecognitionLanguages_(["ro-RO", "en-US"])
    except Exception:
        pass
    handler.performRequests_error_([req], None)
    lines: list[str] = []
    for obs in (req.results() or []):
        cand = obs.topCandidates_(1)
        if cand and len(cand) > 0:
            lines.append(str(cand[0].string()))
    return "\n".join(lines)


def _ocr_vision_graph(image_path: Path) -> dict:
    """Return a minimal OCR graph (tokens with bounding boxes) using Vision.
    The returned shape is compatible with the engine's expected graph: {
        "allTokens": [ {tokenIndex, text, confidence, boundingBox:{x,y,width,height}, pageIndex } ],
        "pages": [ { pageIndex, pageWidth, pageHeight } ],
        "detectedLanguage": "" }
    """
    import Vision, Quartz
    from Foundation import NSURL
    url = NSURL.fileURLWithPath_(str(image_path))
    src = Quartz.CGImageSourceCreateWithURL(url, None)
    if src is None:
        raise RuntimeError(f"Cannot open image {image_path}")
    cg = Quartz.CGImageSourceCreateImageAtIndex(src, 0, None)
    handler = Vision.VNImageRequestHandler.alloc().initWithCGImage_options_(cg, None)
    req = Vision.VNRecognizeTextRequest.alloc().init()
    req.setRecognitionLevel_(Vision.VNRequestTextRecognitionLevelAccurate)
    req.setUsesLanguageCorrection_(True)
    try:
        req.setRecognitionLanguages_( ["ro-RO", "en-US"] )
    except Exception:
        pass
    handler.performRequests_error_([req], None)

    tokens = []
    idx = 0
    for obs in (req.results() or []):
        cand = obs.topCandidates_(1)
        if not cand or len(cand) == 0:
            continue
        txt = str(cand[0].string())
        # boundingBox is a CGRect normalized (x,y,width,height) relative to image, origin at lower-left
        try:
            bb = obs.boundingBox()
            x = float(bb.origin.x)
            y = float(bb.origin.y)
            w = float(bb.size.width)
            h = float(bb.size.height)
        except Exception:
            # fallback: use zeros
            x = y = w = h = 0.0
        try:
            conf = float(cand[0].confidence())
        except Exception:
            conf = 1.0
        tokens.append({
            "tokenIndex": idx,
            "text": txt,
            "confidence": conf,
            "boundingBox": {"x": x, "y": y, "width": w, "height": h},
            "pageIndex": 0,
            "blockIndex": 0,
            "lineIndex": 0,
            "isBoundingBoxApproximate": False,
        })
        idx += 1

    graph = {
        "allTokens": tokens,
        "pages": [{"pageIndex": 0, "pageWidth": 1.0, "pageHeight": 1.0}],
        "detectedLanguage": "",
    }
    return graph

def _ocr_tesseract(image_path: Path) -> str:
    import pytesseract                                     # pip install pytesseract
    from PIL import Image
    return pytesseract.image_to_string(Image.open(image_path), lang="ron+eng")

def run_ocr(image_path: Path, cache_dir: Path | None) -> str:
    """OCR cu cache: dacă cache_dir/<stem>.txt există, citește; altfel OCR + scrie."""
    if cache_dir is not None:
        cache_dir.mkdir(parents=True, exist_ok=True)
        cached = cache_dir / f"{image_path.stem}.txt"
        if cached.exists():
            return cached.read_text(encoding="utf-8")
    try:
        text = _ocr_vision(image_path)
    except Exception as ev:
        try:
            text = _ocr_tesseract(image_path)
        except Exception as et:
            raise RuntimeError(
                f"Both OCR backends failed for {image_path}:"
                f"\n  Vision: {ev}\n  Tesseract: {et}"
            )
    if cache_dir is not None:
        (cache_dir / f"{image_path.stem}.txt").write_text(text, encoding="utf-8")
    return text

# ─────────────────────────────────────────────────────────────────────────────
#  2. Document type classifier — CoreML (modelul tău) sau fallback keyword
# ─────────────────────────────────────────────────────────────────────────────
class CoreMLDocTypeClassifier:
    """Wraps doc_type_classifier_v1.mlmodelc via coremltools."""

    def __init__(self, model_path: Path):
        import coremltools as ct                           # pip install coremltools
        self.model = ct.models.MLModel(str(model_path))
        spec = self.model.get_spec()
        # First text input feature name.
        self.input_name = next(f.name for f in spec.description.input)
        # Output names.
        self.label_out = spec.description.predictedFeatureName or "label"
        self.prob_out  = spec.description.predictedProbabilitiesName or None

    def predict(self, text: str) -> tuple[str, float]:
        out = self.model.predict({self.input_name: text})
        label = str(out.get(self.label_out, "Unknown"))
        conf = 0.0
        if self.prob_out and self.prob_out in out and isinstance(out[self.prob_out], dict):
            conf = float(out[self.prob_out].get(label, 0.0))
        return label, conf

def _classify_keyword(text: str) -> tuple[str, float]:
    """Paritate cu DocumentTypeClassifier.cs."""
    if not text or not text.strip():
        return "Unknown", 0.0
    u = text.upper()
    if "RP.:" in u or "RP:" in u or "REȚETĂ" in u or "RETETA" in u:
        return "Prescription", 1.0
    if "BILET DE TRIMITERE" in u or "MOTIVUL TRIMITERII" in u:
        return "Referral", 1.0
    if "BULETIN DE ANALIZE" in u or (
        "REZULTAT" in u and ("VALORI DE REFERINȚĂ" in u or "VALORI DE REFERINTA" in u)
    ):
        return "LabResult", 1.0
    if any(k in u for k in (
        "SCRISOARE MEDICALĂ", "SCRISOARE MEDICALA",
        "BILET DE IEȘIRE", "BILET DE IESIRE",
        "EPICRIZĂ", "EPICRIZA",
    )):
        return "Discharge", 1.0
    if "CERTIFICAT MEDICAL" in u or "ADEVERINȚĂ MEDICALĂ" in u or "CONCEDIU MEDICAL" in u:
        return "MedicalCertificate", 1.0
    return "Unknown", 0.0

# ─────────────────────────────────────────────────────────────────────────────
#  3. Heuristic field extraction — port 1:1 din OcrTextProcessingServices.cs
# ─────────────────────────────────────────────────────────────────────────────
CNP_RX = re.compile(r"\b(\d{13})\b")
DATE_RX = re.compile(r"\b(\d{1,2}[./]\d{1,2}[./]\d{4})\b")
NAME_AFTER_LABEL = re.compile(
    r"(?:Nume(?:\s+(?:și|si)\s+prenume(?:le)?)?|Pacient|Numar)\s*:?\s*"
    r"([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})"
)
DOCTOR_RX = re.compile(
    r"(?:Dr\.?|Medic(?:\s+primar)?)\s+"
    r"([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})"
)
DIAGNOSIS_RX = re.compile(
    r"Diagnostic\s*(?:prezumtiv)?\s*:?\s*(.{5,120})", re.IGNORECASE
)
MED_RX = re.compile(
    r"(?:Rp\.?\s*:?\s*)?\d+[\.\)]\s*"
    r"([A-Za-zĂÂÎȘȚăâîșț][\w\s\-]*?)\s+"
    r"(\d+\s*(?:mg|g|mcg|ml)\b)(.*)",
    re.IGNORECASE,
)
LAB_RX = re.compile(
    r"^\s*([A-Za-zĂÂÎȘȚăâîșț][A-Za-zĂÂÎȘȚăâîșț\- \.]{2,40}?)"
    r"\s+([0-9]+(?:[\.,][0-9]+)?)"
    r"\s+([%a-zA-Z/μµ°]+(?:/[a-zA-Z0-9µμ]+)?)\s*$",
    re.MULTILINE,
)

# Additional patterns to catch medications and lab values in looser formats
MED_LINE_RX = re.compile(
    r"([A-Za-zĂÂÎȘȚăâîșț][\w\-\s]{2,60}?)\s*[,;:\-]?\s*(\d+(?:[\.,]\d+)?\s*(?:mg|g|mcg|ml))\b(.*)",
    re.IGNORECASE,
)
LAB_COLON_RX = re.compile(
    r"([A-Za-zĂÂÎȘȚăâîșț][A-Za-z0-9ĂÂÎȘȚăâîșț\-\s\.]{2,60}?)\s*[:\-]\s*([0-9]+(?:[\.,][0-9]+)?)\s*([%a-zA-Z/μµ°]+)",
    re.IGNORECASE,
)
DIAGNOSIS_ALT_RX = re.compile(r"(?:Diag(?:nostic)?|Diagnosticul)\s*[:\-]?\s*(.{5,200})", re.IGNORECASE)

# Document type synonyms (English + Romanian) to make heuristics language-agnostic
PRESCRIPTION_TYPES = {"Prescription", "reteta_medicala", "reteta", "reteta_medicala"}
LAB_TYPES = {"LabResult", "buletin_analize", "analize", "analize_lab", "labresult"}
REFERRAL_TYPES = {"Referral", "bilet_trimitere", "trimitere"}
DISCHARGE_TYPES = {"Discharge", "scrisoare_externare", "externare", "epicriza"}
CONSULTATION_TYPES = {"ConsultationNote", "consultation", "consultatie"}

# Units/ tokens used to identify lab columns or units
LAB_UNIT_RX = re.compile(
    r"(mg\/?dL|g\/?dL|mmol\/?L|%|U\/?L|IU\/?L|mg\/?L|mmol|mU\/?L|μU\/?mL|µU\/?mL|mmol/l|mg/dl|10\\^3\/?uL|10\\^3\/?µL|uIU\/?mL|uiu\/?ml|ng\/?dL|mm\/?h|mm/h|10\^3\/?uL)",
    re.IGNORECASE,
)

def parse_lab_table(text: str) -> list[dict[str, str]]:
    """Try to extract name/value/unit pairs from small OCR tables.

    Heuristic: for each line with a numeric token, treat the prefix as name and
    the numeric+unit as value/unit. If name is too short, use previous non-empty
    line as candidate name. Skip obvious non-lab lines (CNP, Adresă, Vârstă).
    """
    out: list[dict[str, str]] = []
    lines = [l.strip() for l in text.splitlines() if l.strip()]
    for i, line in enumerate(lines):
        # ignore lines that look like whole-table headers
        if len(line) < 4:
            continue
        # find first numeric token
        mnum = re.search(r"\b(\d{1,3}(?:[\.,]\d+)?|\d{2,4})\b", line)
        if not mnum:
            continue
        val = mnum.group(1).replace(',', '.')
        # skip if value looks like CNP (13 digits)
        if re.fullmatch(r"\d{13}", val):
            continue
        # find unit in same line
        unit_m = LAB_UNIT_RX.search(line)
        unit = unit_m.group(1) if unit_m else ""
        # name is text before the numeric token
        name = line[:mnum.start()].strip()
        if len(re.sub(r"\W+", "", name)) < 3 and i > 0:
            prev = re.sub(r"\s+", " ", lines[i-1]).strip()
            if len(re.sub(r"\W+", "", prev)) >= 3:
                name = prev
        # final sanity checks
        if len(re.sub(r"\W+", "", name)) < 3:
            continue
        if not re.match(r"^\d+(?:\.\d+)?$", val):
            continue
        out.append({"name": name, "value": val, "unit": unit})
    return out


def _structured_to_pred(doc: dict) -> dict:
    """Convert engine StructuredMedicalDocument JSON to evaluator prediction shape."""
    if not isinstance(doc, dict):
        return {"document_type": None, "patient_name": None, "patient_cnp": None,
                "date": None, "doctor_name": None, "diagnosis": None,
                "medications": [], "lab_values": []}

    def _get_field(d, *keys):
        cur = d
        for k in keys:
            if not cur:
                return None
            cur = cur.get(k)
        if isinstance(cur, dict):
            return cur.get("value")
        return cur

    meds = []
    for m in doc.get("medications") or []:
        name = _get_field(m, "name") or _get_field(m, "Name")
        dose = _get_field(m, "dose") or _get_field(m, "Dose")
        freq = _get_field(m, "frequency") or _get_field(m, "Frequency")
        if name:
            meds.append({"name": name, "dose": dose or "", "frequency": freq or ""})

    labs = []
    for l in doc.get("labResults") or doc.get("lab_results") or []:
        name = _get_field(l, "analysisName") or _get_field(l, "analysis_name") or _get_field(l, "analysis")
        value = _get_field(l, "value")
        unit = _get_field(l, "unit")
        if name and value:
            labs.append({"name": name, "value": str(value), "unit": unit or ""})

    return {
        "document_type": doc.get("documentType") or doc.get("document_type"),
        "patient_name": _get_field(doc, "patientName") or _get_field(doc, "patient_name"),
        "patient_cnp": _get_field(doc, "patientId") or _get_field(doc, "patient_id"),
        "date": _get_field(doc, "reportDate") or _get_field(doc, "report_date"),
        "doctor_name": _get_field(doc, "doctorName") or _get_field(doc, "doctor_name"),
        "diagnosis": _get_field(doc, "diagnosis"),
        "medications": meds,
        "lab_values": labs,
    }

# common analyte keywords to seed fuzzy lab extraction
ANALYTE_KEYWORDS = [
    "colesterol", "hdl", "ldl", "triglicer", "trigliceride",
    "hemoglob", "leucocit", "trombocit", "vsh", "tsh", "ft4",
    "calciu", "magnezi", "creatin", "uree", "glucoz", "glucoza",
    "tg", "colesterol total", "colesterol HDL", "colesterol LDL",
]
ANALYTE_CANONICAL = {
    "colesterol": "Colesterol total",
    "hdl": "HDL colesterol",
    "ldl": "LDL colesterol",
    "triglicer": "Trigliceride",
    "trigliceride": "Trigliceride",
    "hemoglob": "Hemoglobină",
    "leucocit": "Leucocite",
    "trombocit": "Trombocite",
    "vsh": "VSH",
    "tsh": "TSH",
    "ft4": "FT4",
    "calciu": "Calciu total",
    "magnezi": "Magneziu",
    "creatin": "Creatinină",
    "uree": "Uree",
    "glucoz": "Glucoză",
}

def _first(rx: re.Pattern, text: str) -> str | None:
    m = rx.search(text)
    return m.group(1).strip() if m else None

def extract_heuristic(text: str, doc_type: str) -> dict[str, Any]:
    """Întoarce dict cu aceleași câmpuri ca ground_truth['ground_truth']."""
    medications: list[dict[str, str]] = []
    lab_values:  list[dict[str, str]] = []
    if doc_type in PRESCRIPTION_TYPES:
        # numbered/ordered medication lines (1. Name 5 mg ...)
        for m in MED_RX.finditer(text):
            medications.append({
                "name":      m.group(1).strip(),
                "dose":      m.group(2).strip(),
                "frequency": (m.group(3) or "").strip(),
            })
        # loose lines like "Perindopril 5 mg 1 cp/zi" or "Paracetamol, 500 mg"
        for m in MED_LINE_RX.finditer(text):
            medications.append({
                "name":      m.group(1).strip(),
                "dose":      m.group(2).strip(),
                "frequency": (m.group(3) or "").strip(),
            })
        # Deduplicate by normalized (name,dose)
        seen = set()
        unique_meds: list[dict[str, str]] = []
        for md in medications:
            k = (norm(md.get('name')), re.sub(r"\s+","", md.get('dose','')))
            if k in seen:
                continue
            seen.add(k)
            unique_meds.append(md)
        medications = unique_meds
    if doc_type in LAB_TYPES:
        # strict table-like matches
        for m in LAB_RX.finditer(text):
            lab_values.append({
                "name":  m.group(1).strip(),
                "value": m.group(2).replace(",", ".").strip(),
                "unit":  m.group(3).strip(),
            })
        # loose "Name: 123 mg/dL" patterns
        for m in LAB_COLON_RX.finditer(text):
            lab_values.append({
                "name":  m.group(1).strip(),
                "value": m.group(2).replace(",", ".").strip(),
                "unit":  m.group(3).strip(),
            })
        # try table-aware parsing to capture rows split across lines
        try:
            tbl = parse_lab_table(text)
            if tbl:
                lab_values.extend(tbl)
        except Exception:
            pass
        # keyword-based extraction: find known analytes and grab next numeric + unit
        for kw in ANALYTE_KEYWORDS:
            for m in re.finditer(r"\b" + re.escape(kw) + r"\b", text, re.IGNORECASE):
                # prefer a numeric token on the same line as the keyword
                start = text.rfind('\n', 0, m.start())
                end = text.find('\n', m.end())
                if start < 0:
                    start = 0
                else:
                    start += 1
                if end < 0:
                    end = min(len(text), m.end()+120)
                line = text[start:end]
                mnum = re.search(r"\b(\d{1,3}(?:[\.,]\d+)?)\b", line)
                chosen_unit = ""
                val = None
                if mnum:
                    val = mnum.group(1).replace(',', '.')
                    unit_m = LAB_UNIT_RX.search(line)
                    chosen_unit = unit_m.group(1) if unit_m else ""
                else:
                    # try next line
                    nl_start = end + 1
                    nl_end = min(len(text), nl_start + 120)
                    nextline = text[nl_start:nl_end]
                    mnum = re.search(r"\b(\d{1,3}(?:[\.,]\d+)?)\b", nextline)
                    if mnum:
                        val = mnum.group(1).replace(',', '.')
                        unit_m = LAB_UNIT_RX.search(nextline)
                        chosen_unit = unit_m.group(1) if unit_m else ""
                if not val:
                    continue
                key = kw.lower()
                name = ANALYTE_CANONICAL.get(key, kw.capitalize())
                if len(re.sub(r"\W+", "", name)) < 3:
                    continue
                if not re.match(r"^\d+(?:\.\d+)?$", val):
                    continue
                lab_values.append({"name": name, "value": val, "unit": chosen_unit})
        # dedupe lab values by (name,value) then filter obvious OCR/table artifacts
        seen = set()
        unique_labs: list[dict[str, str]] = []
        for lv in lab_values:
            k = (norm(lv.get('name')), norm(lv.get('value')))
            if k in seen:
                continue
            seen.add(k)
            unique_labs.append(lv)
        lab_values = unique_labs
        # filter by plausible numeric value and known unit tokens
        UNIT_OK = re.compile(r"(mg\/?dL|g\/?dL|mmol\/?L|%|U\/?L|IU\/?L|mg\/?L|mmol|mU\/?L|μU\/?mL|µU\/?mL)", re.IGNORECASE)
        filtered: list[dict[str, str]] = []
        for lv in lab_values:
            name = lv.get('name','').strip()
            value = lv.get('value','').replace(',', '.').strip()
            unit = lv.get('unit','').strip()
            # skip short or non-alpha names (CNP, Adresă, Vârstă etc.)
            if len(re.sub(r"\W+", "", name)) < 3:
                continue
            if not re.match(r"^\d+(?:\.\d+)?$", value):
                continue
            # accept if unit looks valid OR name contains known analyte keywords
            if not UNIT_OK.search(unit):
                if not re.search(r"(colesterol|hemoglob|triglicerid|trombocit|leucocit|creatin|uree|glucoz|vsh|tsh|ft4|calciu|magnezi|ldl|hdl|tg|trigliceride)", name, re.IGNORECASE):
                    continue
            filtered.append({"name": name, "value": value, "unit": unit})
        lab_values = filtered
    diagnosis = None
    if doc_type in (PRESCRIPTION_TYPES | REFERRAL_TYPES | DISCHARGE_TYPES | CONSULTATION_TYPES):
        diagnosis = _first(DIAGNOSIS_RX, text) or _first(DIAGNOSIS_ALT_RX, text)
        if not diagnosis:
            m = re.search(r"(?mi)^\s*(?:Diagnostic(?:\s*prezumtiv)?|Motivul)\s*[:\-]?\s*(.+)$", text)
            if m:
                diagnosis = m.group(1).strip()
    return {
        "document_type": doc_type,
        "patient_name":  _first(NAME_AFTER_LABEL, text),
        "patient_cnp":   _first(CNP_RX, text),
        "date":          _first(DATE_RX, text),
        "doctor_name":   _first(DOCTOR_RX, text),
        "diagnosis":     diagnosis,
        "medications":   medications,
        "lab_values":    lab_values,
    }

# ─────────────────────────────────────────────────────────────────────────────
#  4. Matching helpers (Levenshtein + Romanian-diacritic normalization)
# ─────────────────────────────────────────────────────────────────────────────
_DIA = str.maketrans({
    "ă": "a", "â": "a", "î": "i", "ș": "s", "ț": "t",
    "Ă": "a", "Â": "a", "Î": "i", "Ș": "s", "Ț": "t",
})

def norm(s: str | None) -> str:
    if not s:
        return ""
    s = s.translate(_DIA)
    s = unicodedata.normalize("NFD", s)
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    s = re.sub(r"[^a-zA-Z0-9 ]+", " ", s.lower())
    return re.sub(r"\s+", " ", s).strip()

def lev(a: str, b: str) -> int:
    if a == b:
        return 0
    if not a:
        return len(b)
    if not b:
        return len(a)
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i] + [0] * len(b)
        for j, cb in enumerate(b, 1):
            cur[j] = min(cur[j - 1] + 1, prev[j] + 1, prev[j - 1] + (ca != cb))
        prev = cur
    return prev[-1]

def name_match(expected: str | None, actual: str | None, max_per_token: int = 2) -> bool:
    """Token-level fuzzy match cu Levenshtein <=2 (port din NameMatchingService)."""
    ne, na = norm(expected), norm(actual)
    if not ne or not na:
        return False
    if ne == na:
        return True
    te, ta = ne.split(), na.split()
    if not te or not ta:
        return False

    def subset(small, big):
        used = set()
        for w in small:
            best, bi = 10 ** 9, -1
            for j, w2 in enumerate(big):
                if j in used:
                    continue
                d = lev(w, w2)
                if d < best:
                    best, bi = d, j
            if bi < 0 or best > max_per_token:
                return False
            used.add(bi)
        return True

    return subset(te, ta) or subset(ta, te)

def scalar_match(field_name: str, gt: Any, pred: Any) -> bool:
    if gt is None and pred is None:
        return True
    if gt is None or pred is None:
        return False
    if field_name in ("patient_name", "doctor_name", "diagnosis"):
        return name_match(str(gt), str(pred))
    return norm(str(gt)) == norm(str(pred))   # cnp, date, document_type

# ─────────────────────────────────────────────────────────────────────────────
#  5. Metric accumulator
# ─────────────────────────────────────────────────────────────────────────────
SCALAR_FIELDS = ["patient_name", "patient_cnp", "date", "doctor_name", "diagnosis"]
LIST_FIELDS   = ["medications", "lab_values"]

@dataclass
class Counts:
    tp: int = 0
    fp: int = 0
    fn: int = 0

    def add(self, tp=0, fp=0, fn=0):
        self.tp += tp
        self.fp += fp
        self.fn += fn

    def metrics(self) -> dict[str, float]:
        p  = self.tp / (self.tp + self.fp) if (self.tp + self.fp) else 0.0
        r  = self.tp / (self.tp + self.fn) if (self.tp + self.fn) else 0.0
        f1 = 2 * p * r / (p + r) if (p + r) else 0.0
        return {"P": p, "R": r, "F1": f1, "TP": self.tp, "FP": self.fp, "FN": self.fn}

def med_key(m: dict) -> tuple[str, str]:
    return (norm(m.get("name", "")), norm(re.sub(r"\s+", "", str(m.get("dose", "")))))

def lab_key(l: dict) -> tuple[str, str]:
    return (norm(l.get("name", "")), norm(re.sub(r"\s+", "", str(l.get("value", "")))))

def score_list(gt_items: list[dict], pred_items: list[dict], key_fn) -> Counts:
    """Micro: TP = perechi care matchează după key; FP / FN restul."""
    c = Counts()
    gt_keys   = [key_fn(x) for x in (gt_items or [])]
    pred_keys = [key_fn(x) for x in (pred_items or [])]
    used: set[int] = set()
    for pk in pred_keys:
        matched = False
        for i, gk in enumerate(gt_keys):
            if i in used:
                continue
            if pk[1] == gk[1] and name_match(pk[0], gk[0]):
                c.add(tp=1)
                used.add(i)
                matched = True
                break
        if not matched:
            c.add(fp=1)
    c.add(fn=len(gt_keys) - len(used))
    return c

def score_scalar(field_name: str, gt: Any, pred: Any) -> Counts:
    c = Counts()
    has_gt   = gt   not in (None, "", [])
    has_pred = pred not in (None, "", [])
    if has_gt and has_pred:
        if scalar_match(field_name, gt, pred):
            c.add(tp=1)
        else:
            c.add(fp=1, fn=1)
    elif has_gt and not has_pred:
        c.add(fn=1)
    elif has_pred and not has_gt:
        c.add(fp=1)
    return c

# ─────────────────────────────────────────────────────────────────────────────
#  6. Main
# ─────────────────────────────────────────────────────────────────────────────
def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--data-dir",  type=Path, default=Path("data"),
                    help="Folder cu imaginile .png (numite ca în ground_truth).")
    ap.add_argument("--gt",        type=Path, default=Path("data/ground_truth_all.json"))
    ap.add_argument("--model",     type=Path, default=None,
                    help="Path la doc_type_classifier_v1.mlmodelc. Dacă lipsește, "
                         "se folosește keyword-classifier ca fallback.")
    ap.add_argument("--ocr-cache", type=Path, default=Path("data/ocr_cache"))
    ap.add_argument("--out-dir",   type=Path, default=Path("results"))
    ap.add_argument("--native-host", type=Path, default=None,
                    help="Path to DigitalTwin.Mobile.NativeHost.dylib to use NativeAOT extraction")
    ap.add_argument("--limit",     type=int,  default=0,
                    help="Procesează doar primele N documente (0 = toate).")
    args = ap.parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)

    # Load ground truth
    gt_entries = json.loads(args.gt.read_text(encoding="utf-8"))
    if args.limit:
        gt_entries = gt_entries[: args.limit]

    # Load classifier (CoreML if available, else keyword fallback)
    clf = None
    if args.model and args.model.exists():
        try:
            clf = CoreMLDocTypeClassifier(args.model)
            print(f"[clf] Loaded CoreML model: {args.model}", file=sys.stderr)
        except Exception as e:
            print(f"[clf] CoreML load failed ({e}); using keyword fallback.", file=sys.stderr)
    else:
        print("[clf] No --model given; using keyword fallback "
              "(DocumentTypeClassifier.cs parity).", file=sys.stderr)

    # NativeHost bridge (optional)
    bridge = None
    if args.native_host:
        # Resolve the Bridge class without rebinding the module-level name
        if NativeHostBridge is None:
            try:
                from scripts.nativehost_bridge import NativeHostBridge as _BridgeClass  # type: ignore
            except Exception:
                try:
                    from nativehost_bridge import NativeHostBridge as _BridgeClass
                except Exception as e:
                    print(f"[native] failed to import NativeHostBridge: {e}", file=sys.stderr)
                    _BridgeClass = None
            BridgeClass = _BridgeClass
        else:
            BridgeClass = NativeHostBridge

        if BridgeClass is None:
            print("[native] NativeHostBridge not available; skipping native host.", file=sys.stderr)
        else:
            print(f"[native] loading native host: {args.native_host}", file=sys.stderr)
            bridge = BridgeClass(str(args.native_host))
            # initialize with a temporary DB path in out_dir
            db_path = str((args.out_dir / "native_db").resolve())
            os.makedirs(db_path, exist_ok=True)
            init_res = bridge.initialize(db_path, "", "", "", "", "", "")
            print(f"[native] init result: {init_res}", file=sys.stderr)

    # Counters for CoreML pipeline (predicted doc_type)
    global_scalar = {f: Counts() for f in SCALAR_FIELDS}
    global_list   = {f: Counts() for f in LIST_FIELDS}
    by_type: dict[str, dict[str, Counts]] = {}
    # Counters for Heuristic-only pipeline (using GT doc_type)
    global_scalar_gt = {f: Counts() for f in SCALAR_FIELDS}
    global_list_gt   = {f: Counts() for f in LIST_FIELDS}
    by_type_gt: dict[str, dict[str, Counts]] = {}
    cls_counts   = Counts()   # classification counts (pred vs gt)
    predictions  = []
    per_doc_rows = []
    per_doc_rows_gt = []

    for entry in gt_entries:
        stem = entry["stem"]
        gt   = entry["ground_truth"]
        img  = args.data_dir / f"{stem}.png"
        if not img.exists():
            alt = args.data_dir / "images" / f"{stem}.png"
            img = alt if alt.exists() else img
        if not img.exists():
            print(f"[skip] image missing for {stem}: {img}", file=sys.stderr)
            continue

        # 1. OCR
        text = run_ocr(img, args.ocr_cache)
        # Build OCR token graph (cached) for full-fidelity extraction
        graph = None
        try:
            if args.ocr_cache is not None:
                gpath = args.ocr_cache / f"{img.stem}.graph.json"
                if gpath.exists():
                    graph = json.loads(gpath.read_text(encoding="utf-8"))
                else:
                    graph = _ocr_vision_graph(img)
                    gpath.parent.mkdir(parents=True, exist_ok=True)
                    gpath.write_text(json.dumps(graph, ensure_ascii=False), encoding="utf-8")
            else:
                graph = _ocr_vision_graph(img)
        except Exception as e:
            print(f"[ocr] failed to build token graph for {img}: {e}", file=sys.stderr)
            graph = None
        # Convert Vision bbox (origin bottom-left) to engine expected (top-left)
        if graph is not None:
            try:
                for t in graph.get("allTokens", []):
                    bb = t.get("boundingBox", {})
                    if bb and all(k in bb for k in ("x", "y", "width", "height")):
                        y = float(bb["y"])
                        h = float(bb["height"])
                        bb["y"] = 1.0 - (y + h)
            except Exception:
                pass
        # 2. Classify
        if clf is not None:
            doc_type, conf = clf.predict(text)
        else:
            doc_type, conf = _classify_keyword(text)
        # Normalize doc_type to engine-expected values
        def _normalize_doc_type(dt: str) -> str:
            if not dt:
                return dt
            u = dt.lower()
            if "analiz" in u or "analize" in u or "lab" in u or "buletin" in u or "analize" in u:
                return "LabResult"
            if "retet" in u or "rp" in u or "prescript" in u or "reteta" in u:
                return "Prescription"
            if "trimiter" in u or "trimitere" in u or "refer" in u:
                return "Referral"
            if "externare" in u or "epicriz" in u or "epicriza" in u or "discharge" in u:
                return "Discharge"
            return dt
        engine_doc_type = _normalize_doc_type(doc_type)
        # 3. Extraction: either NativeHost structured extractor or local heuristic
        if bridge is not None:
            # Build input JSON
            in_json = {
                "documentId": str(uuid.uuid4()),
                "ocrText": text,
                "graph": graph,
                "docType": engine_doc_type,
                "classConfidence": float(conf),
                "classMethod": ("coreml" if clf is not None else "keyword"),
                "useMlExtraction": True,
                "ocrDurationMs": 0,
                "classificationDurationMs": 0,
            }
            doc = bridge.build_structured_document_json(in_json)
            # Save debug artifacts: input and native output JSON
            try:
                debug_dir = Path(args.out_dir) / "native_debug"
                debug_dir.mkdir(parents=True, exist_ok=True)
                (debug_dir / f"{stem}.clf.in.json").write_text(json.dumps(in_json, ensure_ascii=False), encoding="utf-8")
                (debug_dir / f"{stem}.clf.out.json").write_text(json.dumps(doc, ensure_ascii=False), encoding="utf-8")
            except Exception:
                pass
            pred_clf = _structured_to_pred(doc)
            pred_clf["_confidence"] = conf
            # Map predicted document_type (English/variants) to GT canonical keys
            def _pred_to_gt_doc_type(dt: str | None) -> str | None:
                if not dt:
                    return dt
                if dt in PRESCRIPTION_TYPES:
                    return "reteta_medicala"
                if dt in LAB_TYPES:
                    return "buletin_analize"
                if dt in REFERRAL_TYPES:
                    return "bilet_trimitere"
                if dt in DISCHARGE_TYPES:
                    return "scrisoare_externare"
                if dt in CONSULTATION_TYPES:
                    return "consultatie"
                return dt
            pred_clf["document_type"] = _pred_to_gt_doc_type(pred_clf.get("document_type") or engine_doc_type or doc_type)
            # also build using GT doc_type for heuristic-only comparison
            in_json_gt = dict(in_json)
            in_json_gt["documentId"] = str(uuid.uuid4())
            in_json_gt["docType"] = _normalize_doc_type(gt["document_type"])
            doc_gt = bridge.build_structured_document_json(in_json_gt)
            try:
                debug_dir = Path(args.out_dir) / "native_debug"
                debug_dir.mkdir(parents=True, exist_ok=True)
                (debug_dir / f"{stem}.gt.in.json").write_text(json.dumps(in_json_gt, ensure_ascii=False), encoding="utf-8")
                (debug_dir / f"{stem}.gt.out.json").write_text(json.dumps(doc_gt, ensure_ascii=False), encoding="utf-8")
            except Exception:
                pass
            pred_gt = _structured_to_pred(doc_gt)
        else:
            # 3. Heuristic extract
            pred_clf = extract_heuristic(text, doc_type)
            pred_clf["_confidence"] = conf
            pred_gt = extract_heuristic(text, gt["document_type"])  # heuristic with correct doc type
        predictions.append({"stem": stem, "gt_type": gt["document_type"], "pred_clf": pred_clf, "pred_gt": pred_gt})

        # 4a. Classification score (predicted doc_type vs GT)
        if pred_clf["document_type"] == gt["document_type"]:
            cls_counts.add(tp=1)
        else:
            cls_counts.add(fp=1, fn=1)

        # 4b. Score CoreML pipeline (use pred_clf)
        bt = by_type.setdefault(
            gt["document_type"],
            {f: Counts() for f in SCALAR_FIELDS + LIST_FIELDS},
        )
        for f in SCALAR_FIELDS:
            c = score_scalar(f, gt.get(f), pred_clf.get(f))
            global_scalar[f].add(c.tp, c.fp, c.fn)
            bt[f].add(c.tp, c.fp, c.fn)
            per_doc_rows.append({
                "stem": stem, "doc_type": gt["document_type"],
                "field": f, "gt": gt.get(f), "pred": pred_clf.get(f),
                "tp": c.tp, "fp": c.fp, "fn": c.fn,
            })
        for f, key_fn in (("medications", med_key), ("lab_values", lab_key)):
            c = score_list(gt.get(f) or [], pred_clf.get(f) or [], key_fn)
            global_list[f].add(c.tp, c.fp, c.fn)
            bt[f].add(c.tp, c.fp, c.fn)
            per_doc_rows.append({
                "stem": stem, "doc_type": gt["document_type"], "field": f,
                "gt":   json.dumps(gt.get(f),   ensure_ascii=False),
                "pred": json.dumps(pred_clf.get(f), ensure_ascii=False),
                "tp": c.tp, "fp": c.fp, "fn": c.fn,
            })

        # 4c. Score Heuristic-only pipeline (use pred_gt)
        btg = by_type_gt.setdefault(
            gt["document_type"],
            {f: Counts() for f in SCALAR_FIELDS + LIST_FIELDS},
        )
        for f in SCALAR_FIELDS:
            c = score_scalar(f, gt.get(f), pred_gt.get(f))
            global_scalar_gt[f].add(c.tp, c.fp, c.fn)
            btg[f].add(c.tp, c.fp, c.fn)
            per_doc_rows_gt.append({
                "stem": stem, "doc_type": gt["document_type"],
                "field": f, "gt": gt.get(f), "pred": pred_gt.get(f),
                "tp": c.tp, "fp": c.fp, "fn": c.fn,
            })
        for f, key_fn in (("medications", med_key), ("lab_values", lab_key)):
            c = score_list(gt.get(f) or [], pred_gt.get(f) or [], key_fn)
            global_list_gt[f].add(c.tp, c.fp, c.fn)
            btg[f].add(c.tp, c.fp, c.fn)
            per_doc_rows_gt.append({
                "stem": stem, "doc_type": gt["document_type"], "field": f,
                "gt":   json.dumps(gt.get(f),   ensure_ascii=False),
                "pred": json.dumps(pred_gt.get(f), ensure_ascii=False),
                "tp": c.tp, "fp": c.fp, "fn": c.fn,
            })

    # ── Persist predictions
    (args.out_dir / "predictions.json").write_text(
        json.dumps(predictions, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    with (args.out_dir / "per_doc_coreml.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=[
            "stem", "doc_type", "field", "gt", "pred", "tp", "fp", "fn",
        ])
        w.writeheader()
        w.writerows(per_doc_rows)
    with (args.out_dir / "per_doc_heuristic.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=[
            "stem", "doc_type", "field", "gt", "pred", "tp", "fp", "fn",
        ])
        w.writeheader()
        w.writerows(per_doc_rows_gt)

    # ── Tabelul 2 (global)
    print("\n══════════════════ Tabelul 2 — Heuristic-only pipeline (using CoreML doc_type) ══════════════════")
    print(f"{'Câmp':<22}{'P':>8}{'R':>8}{'F1':>8}{'TP':>6}{'FP':>6}{'FN':>6}")
    print("─" * 64)
    table_rows = []
    m = cls_counts.metrics()
    print(f"{'document_type':<22}{m['P']:>8.3f}{m['R']:>8.3f}{m['F1']:>8.3f}"
          f"{m['TP']:>6}{m['FP']:>6}{m['FN']:>6}")
    table_rows.append({"field": "document_type", **m})
    for f in SCALAR_FIELDS + LIST_FIELDS:
        c = global_scalar.get(f) or global_list.get(f)
        m = c.metrics()
        print(f"{f:<22}{m['P']:>8.3f}{m['R']:>8.3f}{m['F1']:>8.3f}"
              f"{m['TP']:>6}{m['FP']:>6}{m['FN']:>6}")
        table_rows.append({"field": f, **m})
    with (args.out_dir / "table2_metrics_coreml.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=["field", "P", "R", "F1", "TP", "FP", "FN"])
        w.writeheader()
        w.writerows(table_rows)

    # ── Also print metrics for Heuristic-only pipeline (using GT doc_type)
    print("\n══════════════════ Tabelul 2 — Heuristic-only (GT doc_type) ══════════════════")
    table_rows_gt = []
    for f in ("document_type",) + tuple(SCALAR_FIELDS + LIST_FIELDS):
        if f == "document_type":
            m = cls_counts.metrics()
            print(f"{'document_type':<22}{m['P']:>8.3f}{m['R']:>8.3f}{m['F1']:>8.3f}"
                  f"{m['TP']:>6}{m['FP']:>6}{m['FN']:>6}")
            table_rows_gt.append({"field": "document_type", **m})
            continue
        c = global_scalar_gt.get(f) or global_list_gt.get(f)
        m = c.metrics()
        print(f"{f:<22}{m['P']:>8.3f}{m['R']:>8.3f}{m['F1']:>8.3f}"
              f"{m['TP']:>6}{m['FP']:>6}{m['FN']:>6}")
        table_rows_gt.append({"field": f, **m})
    with (args.out_dir / "table2_metrics_heuristic.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=["field", "P", "R", "F1", "TP", "FP", "FN"])
        w.writeheader()
        w.writerows(table_rows_gt)

    # ── Tabelul 2 spart pe tip de document
    by_rows = []
    for dt, fields in by_type.items():
        for f, c in fields.items():
            m = c.metrics()
            by_rows.append({"doc_type": dt, "field": f, **m})
    with (args.out_dir / "table2_by_doctype.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=[
            "doc_type", "field", "P", "R", "F1", "TP", "FP", "FN",
        ])
        w.writeheader()
        w.writerows(by_rows)
    # Write a human-readable explanation of Table 2 and CSV artifacts
    try:
        readme = args.out_dir / "table2_readme.md"
        readme.write_text(
            """
# Table 2 — Metrics and CSV artifacts

This folder contains evaluation artifacts produced by `evaluate_pipeline.py`.

Files:

- `predictions.json`: Full list of predictions and intermediate payloads per document.
- `per_doc_coreml.csv`: Per-document rows used to compute the global metrics for the pipeline that uses the CoreML classifier. Columns:
  - `stem`: document id (filename stem)
  - `doc_type`: ground-truth document type
  - `field`: the evaluated field (e.g. `patient_name`, `medications`, `lab_values`)
  - `gt`: ground-truth value for the field
  - `pred`: predicted value for the field
  - `tp`: 1 if this document contributed a true positive for this field, else 0
  - `fp`: 1 if false positive, else 0
  - `fn`: 1 if false negative, else 0

- `per_doc_heuristic.csv`: Same as above but for the heuristic-only extractor (using GT doc_type).

- `table2_metrics_coreml.csv`: Global aggregated metrics for Table 2 (pipeline using CoreML doc_type). Columns:
  - `field`: evaluated field
  - `P`: Precision = TP / (TP + FP)
  - `R`: Recall = TP / (TP + FN)
  - `F1`: F1 score = 2PR/(P+R)
  - `TP`: total true positives
  - `FP`: total false positives
  - `FN`: total false negatives

- `table2_metrics_heuristic.csv`: Global aggregated metrics for the heuristic-only pipeline (GT doc_type). Same columns as above.

- `table2_by_doctype.csv`: Table 2 metrics broken down by `doc_type` and `field`.

Notes:

- Precision (`P`) measures how many predicted items are correct.
- Recall (`R`) measures how many ground-truth items were recovered.
- F1 is the harmonic mean of precision and recall.
- For scalar fields (e.g., `patient_name`, `doctor_name`, `diagnosis`) fuzzy token-level string matching with Levenshtein tolerance is used.
- For list fields (`medications`, `lab_values`) micro-averaging is used: matches are counted by item-level keys (name + dose/value).

If you need a different CSV layout or additional columns (e.g., per-field support counts), tell me which columns to include and I will add them.
""",
            encoding="utf-8",
        )
    except Exception:
        pass

    print(f"\n→ artefacts in {args.out_dir.resolve()}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
