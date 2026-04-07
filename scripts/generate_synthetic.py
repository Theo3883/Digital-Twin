#!/usr/bin/env python3
"""
generate_synthetic.py — Step C of the training data pipeline.

Generates synthetic Romanian medical document OCR-like text examples per class and
writes them to models/training_data/<ClassName>/.

Providers:
  - openrouter: uses OpenRouter Chat Completions API (recommended)
  - gemini:     uses google-generativeai (legacy; may hit quota / deprecation)
  - offline:    template-based generator (no network)

IMPORTANT:
  - Developer-only. This runs offline during model training.
  - The app NEVER calls any LLM APIs.

Env vars (repo root .env):
  - OPENROUTER_API_KEY=...
  - OPENROUTER_MODEL=openai/gpt-oss-20b  (default)
  - GEMINI_API_KEY=...

Usage:
  python scripts/generate_synthetic.py --provider openrouter
  python scripts/generate_synthetic.py --provider offline --count 30
"""

import argparse
import pathlib
import random
import string
import sys
import time
from typing import Literal

TRAINING_DIR = pathlib.Path("models/training_data")

# Supported document classes with their Romanian identity keywords
CLASS_PROMPTS: dict[str, dict] = {
    "Prescription": {
        "header": "REȚETĂ MEDICALĂ",
        "description": "Romanian prescription (Rețetă medicală) listing medications with dosage and frequency",
        "examples": ["Rp.: 1. Aspenter 75mg, 1cp/zi, dimineata. 2. Betaloc ZOK 50mg, 1/2cp/zi dim."],
    },
    "Referral": {
        "header": "BILET DE TRIMITERE",
        "description": "Romanian referral (Bilet de trimitere) to a specialist with reason and presumptive diagnosis",
        "examples": ["Motivul trimiterii: palpitații frecvente. Diagnostic prezumtiv: Aritmie cardiacă."],
    },
    "LabResult": {
        "header": "BULETIN DE ANALIZE MEDICALE",
        "description": "Romanian lab result (Buletin de analize) with a table of analysis names, values, units, and reference ranges",
        "examples": ["Hemoglobina: 14.5 g/dL (ref: 12.0–16.0). Glicemie: 98 mg/dL (ref: 70–100)."],
    },
    "Discharge": {
        "header": "SCRISOARE MEDICALĂ / BILET DE IEȘIRE DIN SPITAL",
        "description": "Romanian hospital discharge letter (Scrisoare medicală / Epicriză) with diagnosis, treatment, and recommendations",
        "examples": ["Diagnostic: HTA gr. II (I10). Tratament: Enalapril 10mg, Atenolol 50mg."],
    },
    "MedicalCertificate": {
        "header": "CERTIFICAT MEDICAL",
        "description": "Romanian medical certificate (Certificat medical / Adeverință medicală) confirming a diagnosis or fitness status",
        "examples": ["Certificam că numitul Ionescu M. este apt/inapt pentru activitate."],
    },
    "ImagingReport": {
        "header": "ECOGRAFIE",
        "description": "Romanian imaging report (Ecografie, Radiografie, CT, or RMN) with findings description",
        "examples": ["Ecografie abdominala: ficat normoecogen, fara leziuni focale."],
    },
    "EcgReport": {
        "header": "ELECTROCARDIOGRAMĂ",
        "description": "Romanian ECG report with rhythm, heart rate, axis, and interpretation",
        "examples": ["Ritm sinusal regulat. Frecventa cardiaca: 72 bpm. Axa electrica normala."],
    },
    "OperativeReport": {
        "header": "PROTOCOL OPERATOR",
        "description": "Romanian operative/surgical report (Protocol operator) with procedure details",
        "examples": ["Interventie: Colecistectomie laparoscopica. Anestezie: generala."],
    },
    "ConsultationNote": {
        "header": "CONSULTAȚIE DE SPECIALITATE",
        "description": "Romanian specialist consultation note with clinical exam findings and diagnosis",
        "examples": ["Examen clinic: TA 130/80 mmHg. Diagnostic: Cardiopatie ischemica cronica."],
    },
    "GenericClinicForm": {
        "header": "FORMULAR CLINIC",
        "description": "Generic Romanian clinic form or administrative medical document",
        "examples": ["Formular de consimtamant / Fisa de observatie clinica."],
    },
}

PROMPT_TEMPLATE = """Generate {count} realistic Romanian medical document text examples of type "{class_name}" ({description}).

Rules:
- Start EACH example with the exact Romanian header: "{header}"
- Include realistic but ENTIRELY FICTIONAL patient data: Romanian-style name, 13-digit CNP (format like 5040308226720), doctor name, date DD.MM.YYYY
- Include plausible body content appropriate to this document type
- Vary the wording, field order, and abbreviations across examples
- In 3 of the 15 examples, simulate OCR noise: substitute ș→s, ă→a, ț→t in a few words
- Keep each example between 5 and 20 lines
- Separate each example with a line containing only: ---

Example body style:
{examples}

Output PLAIN TEXT only — no JSON, no markdown, no numbering.
Start directly with the first document header.
"""


def load_env():
    """Load .env from repo root."""
    try:
        from dotenv import load_dotenv
        load_dotenv(dotenv_path=pathlib.Path(".env"))
    except ImportError:
        print("WARNING: python-dotenv not installed. Reading .env manually.")
        env_file = pathlib.Path(".env")
        if env_file.exists():
            for line in env_file.read_text().splitlines():
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    k, v = line.split("=", 1)
                    import os
                    os.environ.setdefault(k.strip(), v.strip())

def rand_name() -> str:
    first = random.choice(["Ion", "Maria", "Andrei", "Elena", "Mihai", "Ioana", "Teodor", "Ana", "Vlad", "Irina"])
    last = random.choice(["Popescu", "Ionescu", "Dumitru", "Stan", "Radu", "Matei", "Marin", "Georgescu", "Toma"])
    return f"{last} {first}"


def rand_doctor() -> str:
    return f"Dr. {rand_name()}"


def rand_cnp() -> str:
    # 13 digits, realistic-ish; not a real person
    return str(random.randint(1, 8)) + "".join(random.choice(string.digits) for _ in range(12))


def rand_date() -> str:
    dd = random.randint(1, 28)
    mm = random.randint(1, 12)
    yyyy = random.randint(2018, 2026)
    return f"{dd:02d}.{mm:02d}.{yyyy}"


def maybe_ocr_noise(text: str) -> str:
    # very light noise to simulate OCR diacritics loss
    subs = {"ș": "s", "ă": "a", "ț": "t", "Ș": "S", "Ă": "A", "Ț": "T"}
    out = []
    for ch in text:
        if ch in subs and random.random() < 0.08:
            out.append(subs[ch])
        else:
            out.append(ch)
    return "".join(out)


def generate_offline_example(class_name: str, info: dict) -> str:
    header = info["header"]
    patient = rand_name()
    doctor = rand_doctor()
    cnp = rand_cnp()
    date = rand_date()

    if class_name == "Prescription":
        meds = [
            ("Aspenter", "75 mg", "1 cp/zi dimineața"),
            ("Betaloc ZOK", "50 mg", "1/2 cp/zi"),
            ("Enalapril", "10 mg", "1 cp/zi"),
            ("Metformin", "500 mg", "1 cp x 2/zi"),
        ]
        random.shuffle(meds)
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            f"Medic: {doctor}",
            "Rp.:",
        ] + [f"{i+1}. {m} {d}, {f}." for i, (m, d, f) in enumerate(meds[: random.randint(2, 4)])]
    elif class_name == "Referral":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            f"Medic trimițător: {doctor}",
            "Bilet de trimitere către: Cardiologie",
            "Motivul trimiterii: durere toracică / palpitații / dispnee de efort",
            "Diagnostic prezumtiv: HTA / cardiopatie ischemică / aritmie",
            "Recomandări: consult + EKG + analize uzuale",
        ]
    elif class_name == "LabResult":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data recoltării: {date}",
            "ANALIZA      REZULTAT    U.M.     VALORI DE REFERINȚĂ",
            f"Hemoglobina  {random.uniform(11.0, 16.5):.1f}      g/dL    12.0–16.0",
            f"Glicemie     {random.uniform(65.0, 140.0):.0f}       mg/dL   70–100",
            f"Leucocite    {random.uniform(3500, 12000):.0f}    /µL     4000–10000",
        ]
    elif class_name == "Discharge":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            "Diagnostic: HTA grad II; Dislipidemie",
            "Evoluție: favorabilă",
            "Tratament la externare: Enalapril 10mg, Atorvastatin 20mg",
            "Recomandări: dietă, control cardiologie la 1 lună",
            f"Medic curant: {doctor}",
        ]
    elif class_name == "MedicalCertificate":
        body = [
            f"Nume: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            "Certificăm că pacientul a fost consultat și necesită repaus 3 zile.",
            "Diagnostic: viroză respiratorie / lombalgie",
            f"Medic: {doctor}",
        ]
    elif class_name == "ImagingReport":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            "Examen: Ecografie abdominală",
            "Descriere: ficat cu ecostructură omogenă, fără leziuni focale.",
            "Concluzii: aspect ecografic în limite.",
            f"Medic: {doctor}",
        ]
    elif class_name == "EcgReport":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            "ECG de repaus",
            f"Ritm: sinusal, FC {random.randint(55, 95)} bpm",
            "Axa: normală",
            "Interpretare: fără modificări ischemice acute.",
            f"Medic: {doctor}",
        ]
    elif class_name == "OperativeReport":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data intervenției: {date}",
            "Procedură: apendicectomie / colecistectomie laparoscopică",
            "Anestezie: generală",
            "Evoluție intraoperatorie: fără incidente",
            f"Operator: {doctor}",
        ]
    elif class_name == "ConsultationNote":
        body = [
            f"Nume pacient: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            "Consultație de specialitate: Cardiologie",
            "Examen clinic: TA 130/80, puls 72/min",
            "Diagnostic: HTA / cardiopatie ischemică cronică",
            "Recomandări: monitorizare TA, EKG, analize",
            f"Medic: {doctor}",
        ]
    else:  # GenericClinicForm
        body = [
            f"Nume: {patient}",
            f"CNP: {cnp}",
            f"Data: {date}",
            "Formular clinic / fișă pacient",
            "Secțiune: date administrative",
            f"Medic: {doctor}",
        ]

    text = header + "\n" + "\n".join(body)
    return maybe_ocr_noise(text)


def _split_examples(raw_text: str) -> list[str]:
    return [e.strip() for e in raw_text.split("---") if e.strip()]


def _write_examples(out_dir: pathlib.Path, prefix: str, examples: list[str]) -> int:
    out_dir.mkdir(parents=True, exist_ok=True)
    for i, example in enumerate(examples, start=1):
        out_path = out_dir / f"{prefix}_{i:02d}.txt"
        out_path.write_text(example, encoding="utf-8")
    return len(examples)


def generate_for_class_openrouter(class_name: str, info: dict, out_dir: pathlib.Path, model_name: str, count: int) -> int:
    import os
    api_key = os.getenv("OPENROUTER_API_KEY")
    if not api_key or api_key == "your-openrouter-api-key":
        print("ERROR: OPENROUTER_API_KEY not set in .env")
        sys.exit(1)

    prompt = PROMPT_TEMPLATE.format(
        class_name=class_name,
        description=info["description"],
        header=info["header"],
        examples="\n".join(info["examples"]),
        count=count,
    ).replace("Generate 15", "Generate 15")  # keep template stable

    print(f"  Generating {class_name} (openrouter/{model_name})...", end=" ", flush=True)

    try:
        import requests
    except ImportError:
        print("\nERROR: requests not installed. Install with: pip install requests")
        sys.exit(1)

    url = "https://openrouter.ai/api/v1/chat/completions"
    resp = requests.post(
        url,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            # Optional but nice to include; does not affect functionality
            "X-Title": "DigitalTwin Training Scripts",
        },
        json={
            "model": model_name,
            "temperature": 0.8,
            "messages": [
                {"role": "system", "content": "You generate realistic Romanian medical document OCR-like text for training. Output plain text only."},
                {"role": "user", "content": prompt},
            ],
        },
        timeout=120,
    )

    if resp.status_code >= 400:
        raise RuntimeError(f"OpenRouter error {resp.status_code}: {resp.text[:500]}")

    data = resp.json()
    raw_text = data["choices"][0]["message"]["content"].strip()
    examples = _split_examples(raw_text)

    # Enforce exact count: truncate if too many, top-up if too few.
    if len(examples) > count:
        examples = examples[:count]
    elif len(examples) < count:
        missing = count - len(examples)
        # Top-up with offline examples to avoid extra API calls / quota surprises.
        for _ in range(missing):
            examples.append(generate_offline_example(class_name, info))

    n = _write_examples(out_dir, "synthetic_openrouter", examples)
    print(f"OK ({n} examples written)")
    return n


def generate_for_class_gemini(model, class_name: str, info: dict, out_dir: pathlib.Path, count: int) -> int:
    prompt = PROMPT_TEMPLATE.format(
        class_name=class_name,
        description=info["description"],
        header=info["header"],
        examples="\n".join(info["examples"]),
        count=count,
    )

    print(f"  Generating {class_name} (gemini)...", end=" ", flush=True)
    response = model.generate_content(prompt)
    raw_text = response.text.strip()
    examples = _split_examples(raw_text)
    if len(examples) > count:
        examples = examples[:count]
    elif len(examples) < count:
        missing = count - len(examples)
        for _ in range(missing):
            examples.append(generate_offline_example(class_name, info))

    n = _write_examples(out_dir, "synthetic_gemini", examples)
    print(f"OK ({n} examples written)")
    return n


def main():
    parser = argparse.ArgumentParser(description="Generate synthetic training data (openrouter/gemini/offline).")
    parser.add_argument("--classes", nargs="*", default=list(CLASS_PROMPTS.keys()),
                        help="Classes to generate (default: all)")
    parser.add_argument("--provider", choices=["openrouter", "gemini", "offline"], default="openrouter",
                        help="Provider to use (default: openrouter)")
    parser.add_argument("--count", type=int, default=15,
                        help="Examples per class (default: 15)")
    args = parser.parse_args()

    if args.provider == "offline":
        total = 0
        for class_name in args.classes:
            if class_name not in CLASS_PROMPTS:
                print(f"  SKIP unknown class: {class_name}")
                continue
            out_dir = TRAINING_DIR / class_name
            out_dir.mkdir(parents=True, exist_ok=True)
            print(f"  Generating {class_name} (offline)…", end=" ", flush=True)
            for i in range(1, args.count + 1):
                out_path = out_dir / f"synthetic_offline_{i:02d}.txt"
                out_path.write_text(generate_offline_example(class_name, CLASS_PROMPTS[class_name]), encoding="utf-8")
            print(f"OK ({args.count} examples written)")
            total += args.count

        print(f"\nTotal offline synthetic examples generated: {total}")
        print(f"Written to {TRAINING_DIR}/")
        print("\nNext step: python scripts/check_corpus.py --min-per-class 20")
        return

    load_env()

    import os
    openrouter_model = os.getenv("OPENROUTER_MODEL") or "openai/gpt-oss-20b"

    model = None
    if args.provider == "gemini":
        api_key = os.getenv("GEMINI_API_KEY")
        if not api_key or api_key == "your-gemini-api-key":
            print("ERROR: GEMINI_API_KEY not set in .env")
            sys.exit(1)
        try:
            import google.generativeai as genai
        except ImportError:
            print("ERROR: google-generativeai not installed.")
            print("Install with:  pip install google-generativeai")
            sys.exit(1)
        genai.configure(api_key=api_key)
        model = genai.GenerativeModel("gemini-2.0-flash")

    total = 0
    for class_name in args.classes:
        if class_name not in CLASS_PROMPTS:
            print(f"  SKIP unknown class: {class_name}")
            continue
        out_dir = TRAINING_DIR / class_name
        if args.provider == "openrouter":
            count = generate_for_class_openrouter(class_name, CLASS_PROMPTS[class_name], out_dir, openrouter_model, args.count)
        else:
            count = generate_for_class_gemini(model, class_name, CLASS_PROMPTS[class_name], out_dir, args.count)
        total += count
        # Small delay to respect rate limits
        time.sleep(1.0)

    print(f"\nTotal synthetic examples generated: {total}")
    print(f"Written to {TRAINING_DIR}/")
    print("\nREVIEW the generated files and remove any obviously wrong examples.")
    print("Next step: bash scripts/train.sh")


if __name__ == "__main__":
    main()
