# DigitalTwin — OCR & ML Pipeline: Deep Technical Reference

> **Scope**: Full end-to-end analysis of the document scanning, on-device OCR, ML classification,
> structured extraction, identity verification, encryption, and persistence pipeline as implemented in
> `DigitalTwin.OCR` and `DigitalTwin.Integrations`.
>
> **Date**: April 2026 · **Platform**: iOS / macOS Catalyst (.NET MAUI)

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Entry Points — Three Acquisition Paths](#2-entry-points--three-acquisition-paths)
3. [Stage 0 — Security Bootstrap](#3-stage-0--security-bootstrap)
4. [Stage 1 — Document Normalization](#4-stage-1--document-normalization)
5. [Stage 2 — SHA-256 Integrity Hash](#5-stage-2--sha-256-integrity-hash)
6. [Stage 3 — AES-GCM-256 Vault Encryption](#6-stage-3--aes-gcm-256-vault-encryption)
7. [Stage 4a — On-Device OCR (Apple Vision)](#7-stage-4a--on-device-ocr-apple-vision)
8. [Stage 4b — ML Classification Pipeline](#8-stage-4b--ml-classification-pipeline)
9. [Stage 4c — Structured Field Extraction](#9-stage-4c--structured-field-extraction)
10. [Stage 4d — Identity Verification](#10-stage-4d--identity-verification)
11. [Stage 5 — PII Sanitization](#11-stage-5--pii-sanitization)
12. [Stage 6 — Persistence & Sync Preparation](#12-stage-6--persistence--sync-preparation)
13. [Stage 7 — Medical History Auto-Append](#13-stage-7--medical-history-auto-append)
14. [ML Audit System](#14-ml-audit-system)
15. [Feature Flags & Configuration](#15-feature-flags--configuration)
16. [Data Models Reference](#16-data-models-reference)
17. [Security Architecture](#17-security-architecture)
18. [Performance Characteristics](#18-performance-characteristics)

---

## 1. Architecture Overview

The pipeline is orchestrated by a single stateful ViewModel that chains services in a strict waterfall.
Every stage either succeeds and passes its output forward, or fails fast with a user-visible error.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          OcrSessionViewModel (orchestrator)                      │
│                                                                                 │
│  ┌──────────┐   ┌───────────┐   ┌──────────┐   ┌──────────────────────────┐   │
│  │  Camera  │   │   Files   │   │  Photos  │   │   Entry point chooser    │   │
│  │ Scanner  │   │  Picker   │   │  Library │   │   (3 acquisition paths)   │   │
│  └────┬─────┘   └─────┬─────┘   └────┬─────┘   └────────────┬─────────────┘   │
│       └───────────────┴──────────────┘                       │                 │
│                              │ quarantine file                │                 │
│                              ▼                                │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 1: Normalization         │               │                 │
│             │  (metadata strip + EXIF fix)    │               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ normalized bytes                │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 2: SHA-256 Hash          │               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ sha256 hex                      │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 3: AES-GCM-256 Vault     │               │                 │
│             │  (encrypt + Keychain DEK wrap)  │               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ encrypted descriptor            │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 4a: Apple Vision OCR     │               │                 │
│             │  VNRecognizeTextRequest          │               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ OcrExtractionResult + Graph     │                 │
│                             ▼                                 │                 │
│             ┌───────────────────────────────────────────────┐│                 │
│             │  Stage 4b: ML Classification Orchestrator      ││                 │
│             │  Layer 1: Keyword  Layer 2: NL  Layer 3: FP   ││                 │
│             └───────────────┬───────────────────────────────┘│                 │
│                             │ ClassificationResult            │                 │
│                             ▼                                 │                 │
│             ┌───────────────────────────────────────────────┐│                 │
│             │  Stage 4c: Structured Extraction               ││                 │
│             │  BERT (optional) → Heuristic (always)          ││                 │
│             │  Geometric Table Extractor (lab results)        ││                 │
│             └───────────────┬───────────────────────────────┘│                 │
│                             │ StructuredMedicalDocument       │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 4d: Identity Verification│               │                 │
│             │  Name + CNP must match profile  │               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ pass / IdentityMismatch         │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 5: PII Sanitization      │               │                 │
│             │  Redact CNP, phone, email, dates│               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ sanitizedPreview                │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 6: SQLite Persistence    │               │                 │
│             │  OcrDocumentSyncRecord          │               │                 │
│             └───────────────┬────────────────┘               │                 │
│                             │ savedRecord                     │                 │
│                             ▼                                 │                 │
│             ┌────────────────────────────────┐               │                 │
│             │  Stage 7: History Auto-Append   │               │                 │
│             │  MedicalHistoryEntry + notes    │               │                 │
│             └────────────────────────────────┘               │                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Entry Points — Three Acquisition Paths

All three paths funnel into a single private `ProcessQuarantineFileAsync` method after acquiring
a quarantine file path and MIME type.

```
User taps action
       │
       ├──────────────────────────────────────────────────┐
       │                                                  │
       ▼                    ▼                             ▼
RunCameraSessionAsync   RunFileImportSessionAsync   RunPhotoLibraryImportSessionAsync
       │                    │                             │
┌──────┴──────┐    ┌────────┴──────────┐    ┌────────────┴────────┐
│DocumentScanner│  │ FileImportService  │    │PhotoLibraryImport   │
│ScanAsync()   │  │ PickAndImportAsync()│    │PickAndImportAsync() │
│              │  │                    │    │                     │
│→ camera roll │  │→ PDF/JPEG/PNG from │    │→ PHPicker image     │
│  multi-page  │  │  iOS Files.app     │    │  from Photos.app    │
└──────┬──────┘    └────────┬──────────┘    └────────────┬────────┘
       │                    │                             │
       │   (quarantinePath, mimeType)                     │
       └────────────────────┴─────────────────────────────┘
                            │
                            ▼
             ProcessQuarantineFileAsync(...)
             (stages 1 → 7)
```

**MIME types supported**: `JPEG`, `PNG`, `PDF`

---

## 3. Stage 0 — Security Bootstrap

Before any document can be processed the vault must be initialized and unlocked.
This is a separate lifecycle call, not part of the per-document pipeline.

```
App launch
    │
    ▼
OcrSessionViewModel.InitVaultAsync()
    │
    ├─ VaultService.IsInitialized?
    │       FALSE → create directories + Keychain master key
    │       TRUE  → skip
    │
    ▼
OcrSessionViewModel.UnlockVaultAsync()
    │
    ├─ LocalAuthenticationService.AuthenticateAsync()
    │       → LAContext.EvaluatePolicy(BiometricsOrPasscode)
    │       → Face ID / Touch ID / Optic ID / passcode fallback
    │
    ├─ VaultService.Unlock()
    │       → KeychainKeyStore.RetrieveKey()
    │       → master key loaded into memory
    │
    └─ _isUnlocked = true

Vault directory layout
══════════════════════
{AppDataDirectory}/Library/Application Support/DigitalTwin.OcrVault/
    ├── quarantine/     ← incoming untrusted files (purged after processing)
    ├── encrypted/      ← AES-GCM ciphertext blobs
    ├── manifests/      ← JSON descriptors (nonce, tag, wrapped DEK)
    └── temp/           ← staging area (file-protected, excluded from backup)
```

**Security Mode** (`OcrOptions.SecurityMode`):

| Mode | Behaviour |
|------|-----------|
| `Strict` | Device passcode required to init vault; biometry preferred for unlock |
| `RelaxedDebug` | No passcode requirement (dev/test builds only) |

---

## 4. Stage 1 — Document Normalization

**Service**: `DocumentNormalizationService`  
**Purpose**: Canonical representation, metadata stripping, EXIF correction

```
quarantinePath + mimeType
         │
         ├── PDF ──────────────────────────────────────┐
         │                                             │
         │   PdfKit.PdfDocument.Load()                 │
         │   ↓                                         │
         │   Re-render each page into new PdfDocument   │
         │   (strips hidden metadata, annotations)      │
         │   ↓                                         │
         │   GetDataRepresentation() → byte[]          │
         │                                             │
         ├── JPEG / PNG ────────────────────────────────┤
         │                                             │
         │   CGImageSource.FromUrl()                   │
         │   ↓                                         │
         │   UIImage(cgImage) → EXIF orientation auto- │
         │   corrected by UIKit                         │
         │   ↓                                         │
         │   UIImage.AsJPEG(quality: 0.92) → byte[]   │
         │                                             │
         └─────────────────────────────────────────────┘
                              │
                              ▼
              (normalizedBytes, pageCount, mimeType)
```

> All temporary files are protected with `NSFileProtectionComplete` and explicitly excluded from iCloud backup.

---

## 5. Stage 2 — SHA-256 Integrity Hash

**Service**: `HashingService` (BCL only — `System.Security.Cryptography.SHA256`)

```
normalizedBytes
      │
      ▼
SHA256.HashData(normalizedBytes)  // stackalloc — zero heap allocation
      │
      ▼
sha256HexString  (64-char lowercase hex)
```

The hash is stored in the sync record and allows the server to detect duplicate uploads and verify
document integrity after transmission.

---

## 6. Stage 3 — AES-GCM-256 Vault Encryption

**Services**: `VaultService`, `DocumentEncryptionService`, `KeychainKeyStore`

### Key Hierarchy

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         iOS Secure Enclave / Keychain                   │
│                                                                         │
│   Master Key (MK)  ←─── stored in Keychain (kSecAttrAccessibleAfter   │
│   256-bit CSPRNG        FirstUnlockThisDeviceOnly)                      │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │ retrieved on Unlock()
                                    ▼
          ┌────────────────────────────────────────────────────┐
          │  Per-document DEK generation                        │
          │                                                     │
          │  DEK = RandomNumberGenerator.GetBytes(32)          │
          │                                                     │
          │  WrappedDEK = AES-GCM-256(MK, nonce96, DEK)       │
          │  Layout: nonce(12) ‖ tag(16) ‖ wrapped(32) = 60B  │
          └───────────────────────────────┬────────────────────┘
                                          │
                                          ▼
          ┌────────────────────────────────────────────────────┐
          │  Document encryption                                │
          │                                                     │
          │  ciphertext = AES-GCM-256(DEK, nonce96, plaintext)│
          │  tag (128-bit authentication tag)                  │
          └───────────────────────────────┬────────────────────┘
                                          │
                      ┌───────────────────┴──────────────────────┐
                      │  On-disk layout                           │
                      │                                           │
                      │  encrypted/{documentId:N}                 │
                      │      → raw ciphertext bytes               │
                      │                                           │
                      │  manifests/{documentId:N}.json            │
                      │      → { NonceB64, TagB64,                │
                      │          WrappedDekB64, Sha256,           │
                      │          MimeType, PageCount }            │
                      └───────────────────────────────────────────┘
```

### Decryption Path (inverse)

```
manifest JSON
    │
    ├─ UnwrapKey(WrappedDEK, MK)  →  DEK
    │
    ├─ AesGcm.Decrypt(nonce, ciphertext, tag, DEK)
    │
    └─  plaintext bytes
```

> All AES-GCM operations use BCL (`System.Security.Cryptography.AesGcm`) — zero third-party dependencies.

---

## 7. Stage 4a — On-Device OCR (Apple Vision)

**Service**: `LocalOcrService`  
**Apple API**: `VNRecognizeTextRequest` (Vision framework)  
**Privacy**: Fully local — zero network calls

### Recognition Flow

```
documentPath + mimeType
        │
        ├── PDF ──────────────────────────────────────────────────┐
        │                                                         │
        │   1. Try direct text extraction (PdfKit)                │
        │      TextQualityScore = avg(textDensity per page)       │
        │      If score ≥ 0.35 → use text directly (no OCR noise) │
        │      Else → render each page to UIImage at 2480×3508px  │
        │             (A4 @ 300 DPI) → Vision OCR                  │
        │                                                         │
        ├── JPEG / PNG ────────────────────────────────────────────┤
        │                                                         │
        │   → CGImage → VNImageRequestHandler → OCR               │
        │                                                         │
        └─────────────────────────────────────────────────────────┘
                               │
                               ▼
        VNRecognizeTextRequest (per page/image)
            ├── RecognitionLevel: Accurate (default) or Fast
            ├── Languages: ro-RO, en-US, de-DE, fr-FR
            │   (only supported ones added dynamically)
            │
            └── Callbacks per result:
                    VNRecognizedTextObservation
                        ├── topCandidates(1) → text + confidence
                        └── boundingBox (normalized [0,1] coordinates)

                               │
                               ▼
                   ┌───────────────────────────┐
                   │  Per observation          │
                   │                           │
                   │  OcrTextBlock             │
                   │    .Text (string)         │
                   │    .Confidence (float)    │
                   │    .Lines[]               │
                   └───────────┬───────────────┘
                               │
                ┌──────────────┴────────────────────┐
                │  buildGraph=true?                  │
                │  (enabled when UseMlClassification │
                │   or UseIdentityV2 is true)        │
                │                                    │
                │  → split block text into words     │
                │  → per-word bounding box           │
                │     (falls back to line-level box  │
                │      when per-word box unavailable)│
                │  → OcrToken[]                      │
                │  → OcrGraphPage                    │
                │  → OcrDocumentGraph                │
                └────────────────────────────────────┘
                               │
                               ▼
              OcrExtractionResult
                  .Pages[]:        List<OcrPage>
                  .RawText:        string (all blocks joined)
                  .Graph:          OcrDocumentGraph? (spatial)
                  .DetectedLanguage
                  .IsRomanianSupported
```

### OcrDocumentGraph — Spatial Index

```
OcrDocumentGraph
    │
    ├── Pages[]: OcrGraphPage[]
    │       └── Tokens[]: OcrToken[]
    │               ├── TokenIndex
    │               ├── Text
    │               ├── Confidence
    │               ├── PageIndex, BlockIndex, LineIndex
    │               ├── IsBoundingBoxApproximate
    │               └── BoundingBox: OcrBoundingBox
    │                       ├── X, Y  (top-left, normalized)
    │                       ├── Width, Height
    │                       ├── CenterX = X + Width/2
    │                       └── CenterY = Y + Height/2
    │
    └── AllTokens[]: flattened across pages
```

---

## 8. Stage 4b — ML Classification Pipeline

**Entry**: `ClassificationOrchestrator.ClassifyAsync(ocrText, imagePath)`  
**Output**: `ClassificationResult(Type, Confidence, Method)`

The orchestrator implements a **priority waterfall** across three independent classifiers:

```
ocrText + imagePath?
          │
          ├─────────────────────────────────────┐
          │                                     │
          ▼                                     ▼
   Layer 1: Keyword                     Layer 2: NL Text Classifier
   DocumentTypeClassifier               NlDocumentTypeClassifier
   .Classify(ocrText)                   .ClassifyAsync(ocrText)
   O(1) string match                    Apple NaturalLanguage.NLModel
   no model required                    doc_type_classifier_v1.mlmodelc
          │                                     │
          │  keywordType                        │  nlResult
          │  (Unknown or specific)              │  (Type + Confidence)
          │                                     │
          └──────────────┬──────────────────────┘
                         │
                         ▼
              ┌──────────────────────────────────────────────────────┐
              │              FUSION DECISION LOGIC                    │
              │                                                       │
              │  nlIsConfident = nlResult.Confidence ≥ threshold     │
              │                  AND nlResult.Type ≠ Unknown         │
              │                                                       │
              │  ┌─────────────────────────────────────────────────┐ │
              │  │ keyword ≠ Unknown                                │ │
              │  │   nlIsConfident=false → accept keyword (1.0)    │ │
              │  │     Method = "keyword"                           │ │
              │  │   nlIsConfident=true AND agree → accept keyword  │ │
              │  │     Method = "keyword+nl_agree"                  │ │
              │  │   nlIsConfident=true AND disagree → Unknown      │ │
              │  │     Method = "keyword_vs_nl_disagree"            │ │
              │  └─────────────────────────────────────────────────┘ │
              │                                                       │
              │  ┌─────────────────────────────────────────────────┐ │
              │  │ keyword = Unknown                                │ │
              │  │   nlIsConfident → accept NL result              │ │
              │  │     Method = "nl_model"                         │ │
              │  │   else → Layer 3: Feature Print ───────────┐   │ │
              │  └─────────────────────────────────────────────┼───┘ │
              │                                                │      │
              │                                                ▼      │
              │                               Layer 3: Vision Feature Print     │
              │                               FeaturePrintDocumentClassifier    │
              │                                                       │
              │                               VNGenerateImageFeaturePrint       │
              │                               → 768-float vector (iOS 17+)      │
              │                               → 2048-float vector (iOS 15/16)  │
              │                                                       │
              │                               Cosine distance vs reference JSON │
              │                               MaxCosineDistance = 0.25          │
              │                               confidence = 1 - dist/maxDist     │
              │                               Method = "feature_print"          │
              │                                                       │
              │  ┌─────────────────────────────────────────────────┐ │
              │  │ All layers inconclusive → Unknown (0f)          │ │
              │  │   Method = "all_layers_inconclusive"            │ │
              │  └─────────────────────────────────────────────────┘ │
              └──────────────────────────────────────────────────────┘
                         │
                         ▼
              ClassificationResult(Type, Confidence, Method)
```

### Layer 2 — NL Text Classifier

```
ocrText
   │
   ▼
NLModel.Create(CoreML.MLModel)
   │  model: doc_type_classifier_v1.mlmodelc
   │  source: scripts/train.sh + xcrun coremlcompiler
   │  mode: fully offline, on-device
   │
   ▼
model.GetPredictedLabel(ocrText) → label string
   │
   ▼
ParseLabel(label) → MedicalDocumentType enum
   │
   └── confidence: 0.90f if label found, else 0f
```

> The NL classifier is trained on Romanian medical document text and compiled into a CoreML model bundled in `Resources/Models/`.

### Layer 3 — Vision Feature Print

```
imagePath
   │
   ▼
CGImageSource.FromUrl()
   │
   ▼
VNGenerateImageFeaturePrintRequest
   │  iOS 17+ → Revision 2 → 768-element float vector
   │  iOS 15/16 → Scene Feature Print → 2048-element float vector
   │
   ▼
queryVector (float[])
   │
   ▼
Load reference_feature_prints.json
   (bundled in Resources/Models/)
   { label, vector }[]
   │
   ▼
Cosine distance for each reference:
   dist = 1 - dot(q,r) / (|q| · |r|)
   │
   ▼
Best match: min(dist)
   dist ≤ 0.25 → accept
   confidence = 1 - dist / 0.25
   dist > 0.25 → Unknown
```

### Keyword Layer — `DocumentTypeClassifierService`

The keyword layer is a pure O(1) string matcher. It maps Romanian medical document keywords
to `MedicalDocumentType`:

| Keyword(s) | Document Type |
|---|---|
| `RP.:`, `REȚETĂ MEDICALĂ` | `Prescription` |
| `BILET DE TRIMITERE`, `MOTIVUL TRIMITERII`, `DIAGNOSTIC PREZUMTIV` | `Referral` |
| `BULETIN DE ANALIZE`, `REZULTAT` + `VALORI DE REFERINȚĂ` | `LabResult` |
| `SCRISOARE MEDICALĂ`, `EPICRIZĂ`, `BILET DE IEȘIRE` | `Discharge` |
| `CERTIFICAT MEDICAL`, `CONCEDIU MEDICAL` | `MedicalCertificate` |
| `ECOGRAFIE`, `RADIOGRAFIE`, `TOMOGRAFIE`, `EXAMEN RMN` | `ImagingReport` |
| `ELECTROCARDIOGRAMĂ`, `ECG` + `RITM` | `EcgReport` |
| `PROTOCOL OPERATOR`, `INTERVENȚIE CHIRURGICALĂ` | `OperativeReport` |
| `CONSULTAȚIE DE SPECIALITATE`, `EXAMEN CLINIC` | `ConsultationNote` |

### Supported Document Types (`MedicalDocumentType`)

```
MedicalDocumentType
├── Unknown            = 0  (unrecognized — requires human review)
├── Prescription       = 1  (Rețetă medicală)
├── Referral           = 2  (Bilet de trimitere)
├── LabResult          = 3  (Buletin de analize)
├── Discharge          = 4  (Scrisoare medicală / Epicriză)
├── MedicalCertificate = 5  (Certificat medical)
├── ImagingReport      = 6  (Ecografie, CT, RMN)
├── EcgReport          = 7  (Electrocardiogramă)
├── OperativeReport    = 8  (Protocol operator)
├── ConsultationNote   = 9  (Consultație de specialitate)
└── GenericClinicForm  = 10 (catch-all)
```

---

## 9. Stage 4c — Structured Field Extraction

**Service**: `StructuredDocumentBuilder`  
**Output**: `StructuredMedicalDocument`

The builder orchestrates three extractors in a merge topology. BERT results have priority over
heuristic when the BERT confidence is higher.

```
rawText + docType + ClassificationResult + graph?
          │
          │
          ├──── useMlExtraction=true AND BertFieldExtractor.IsModelAvailable()
          │                │
          │                ▼                9a — BERT NER
          │     ┌──────────────────────────────────────────────────┐
          │     │  BertFieldExtractor.Extract(rawText)              │
          │     │                                                   │
          │     │  WordPieceTokenizer.Tokenize(rawText, max=512)   │
          │     │  → [CLS] + wordpiece tokens + [SEP]              │
          │     │  → inputIds[], tokenToWord[]                     │
          │     │                                                   │
          │     │  CoreML.MLModel.Load(bert_ner_v1.mlmodelc)       │
          │     │  inputs:                                          │
          │     │    input_ids     int32[1, seqLen]                │
          │     │    attention_mask int32[1, seqLen] (all 1s)      │
          │     │    token_type_ids int32[1, seqLen] (all 0s)      │
          │     │                                                   │
          │     │  outputs:                                         │
          │     │    logits  float32[1, seqLen, numLabels=15]      │
          │     │                                                   │
          │     │  Argmax per position → BIO tag                   │
          │     │                                                   │
          │     │  BioTagAssembler.Assemble(tags, scores, tokenToWord)
          │     │  → EntitySpan[]                                   │
          │     │      (StartWord, EndWord, Label, AvgScore)        │
          │     │                                                   │
          │     │  BertExtractionResult:                            │
          │     │    PatientName, DoctorName, Diagnosis,           │
          │     │    PatientId (CNP), Medications[]               │
          │     └──────────────────────┬───────────────────────────┘
          │                            │ bertResult (or null)
          │                            │
          ├──── always                 │
          │     │                      │
          │     ▼                9b — Heuristic Regex
          │     ┌──────────────────────────────────────────────────┐
          │     │  HeuristicFieldExtractor.Extract(rawText, docType)│
          │     │                                                   │
          │     │  Field     Regex                          Conf   │
          │     │  ──────────────────────────────────────────────  │
          │     │  CNP       \b(\d{13})\b                  0.92f  │
          │     │  Name      Nume|Pacient: [A-Z...]{2-40}  0.75f  │
          │     │  Date      \d{1,2}[./]\d{1,2}[./]\d{4}  0.85f  │
          │     │  Doctor    Dr.|Medic: [A-Z...]{2-40}     0.72f  │
          │     │  Diagnosis Diagnostic: .{5-120}          0.70f  │
          │     │            (only for Referral/Discharge/         │
          │     │             ConsultationNote)                     │
          │     │  Medication Rp.:\d. name mg/g/mcg/ml     0.80f │
          │     │            (only for Prescription)               │
          │     └──────────────────────┬───────────────────────────┘
          │                            │ heuristicResult
          │                            │
          └────────────────────────────┘
                                       │
                   ┌───────────────────▼──────────────────────────┐
                   │             MERGE LOGIC                        │
                   │                                               │
                   │  For each field (PatientName, PatientId,     │
                   │  DoctorName, Diagnosis):                      │
                   │                                               │
                   │  Merge(bert?.Field, heuristic.Field):        │
                   │    if bert.Confidence ≥ heuristic.Confidence │
                   │        → use BERT field                       │
                   │    else → use heuristic field                 │
                   │                                               │
                   │  Medications:                                 │
                   │    BERT available AND count > 0 → BERT wins  │
                   │    else → heuristic medications               │
                   │                                               │
                   │  ReportDate: heuristic only (no BERT field)  │
                   └───────────────────┬──────────────────────────┘
                                       │
     docType=LabResult AND graph≠null  │
                   ┌───────────────────▼──────────────────────────┐
                   │     9c — GeometricTableExtractor              │
                   │                                               │
                   │  OcrDocumentGraph token bounding boxes        │
                   │                                               │
                   │  1. ClusterIntoRows(tokens)                  │
                   │     group by CenterY ± RowToleranceY=0.012   │
                   │     sort within row by X                      │
                   │                                               │
                   │  2. FindHeaderRow()                           │
                   │     look for ANALIZA/TEST + REZULTAT/VALOARE │
                   │                                               │
                   │  3. DetermineColumns()                        │
                   │     ANALIZA range, REZULTAT range            │
                   │     UNITATE range, REFERINTA range            │
                   │                                               │
                   │  4. ParseDataRows (below header)             │
                   │     map token CenterX → column               │
                   │     IsOutOfRange: parse lo–hi range          │
                   │                                               │
                   │  ExtractedLabResult[]                        │
                   │    .AnalysisName  (0.85f, BoundingBoxAlign)  │
                   │    .Value         (0.85f, BoundingBoxAlign)  │
                   │    .Unit          (0.80f, BoundingBoxAlign)  │
                   │    .ReferenceRange(0.80f, BoundingBoxAlign)  │
                   │    .IsOutOfRange  (numeric comparison)       │
                   └───────────────────┬──────────────────────────┘
                                       │
                   ┌───────────────────▼──────────────────────────┐
                   │           ReviewFlag Generation               │
                   │                                               │
                   │  Field             Condition        Severity  │
                   │  ─────────────────────────────────────────── │
                   │  patient_name  NeedsReview (conf<0.70) Critical│
                   │  patient_id    NeedsReview           Critical  │
                   │  patient_name  not found             Warning   │
                   │  diagnosis     NeedsReview           Warning   │
                   │  analysis_value NeedsReview (per row) Warning  │
                   └───────────────────┬──────────────────────────┘
                                       │
                                       ▼
                   StructuredMedicalDocument
```

### BERT BIO Label Set

The BERT model (`bert_ner_v1.mlmodelc`) is trained with 15 BIO labels:

```
Index  Label               Entity type
  0    O                   Outside (non-entity)
  1    B-PATIENT_NAME      Begin patient name
  2    I-PATIENT_NAME      Inside patient name
  3    B-DOCTOR_NAME       Begin doctor name
  4    I-DOCTOR_NAME       Inside doctor name
  5    B-DATE              Begin date
  6    I-DATE              Inside date
  7    B-DIAGNOSIS         Begin diagnosis
  8    I-DIAGNOSIS         Inside diagnosis
  9    B-MEDICATION        Begin medication
 10    I-MEDICATION        Inside medication
 11    B-CNP               Begin Romanian personal ID (CNP)
 12    I-CNP               Inside CNP
 13    B-FACILITY          Begin facility/hospital name
 14    I-FACILITY          Inside facility name
```

### BIO Tag Assembly

```
Input:  token-level tags from BERT argmax
        tags[] = ["O","B-DIAGNOSIS","I-DIAGNOSIS","I-DIAGNOSIS","O","B-CNP","O",...]
        tokenToWord[] = [-1, 3, 4, 5, -1, 8, -1, ...]  (-1 = [CLS]/[SEP])

Process:
  for each token i:
    tag = "B-X" → flush current span, start new span at wordIdx
    tag = "I-X" AND same label AND consecutive wordIdx → extend span
    tag = "O"   → flush current span

Output: EntitySpan[]
  { StartWord=3, EndWord=5, Label="DIAGNOSIS", AvgScore=0.87 }
  { StartWord=8, EndWord=8, Label="CNP",       AvgScore=0.95 }
```

### WordPiece Tokenizer

```
Input text  →  BasicTokenize (whitespace + punctuation split)
                │
                ▼
              For each word:
                WordPieceTokenizeWord(word.ToLowerInvariant())
                  find longest prefix in vocab
                  remaining suffix → "##" prefix + longest match
                  if no match → [UNK]
                │
                ▼
              [CLS] + piece_ids + [SEP]   (max 512 tokens)
              tokenToWord mapping: piece → original word index
```

Vocab file: `bert_vocab.txt` bundled in app. Falls back to stub vocab
(all words → `[UNK]`) when absent — BERT can still run with degraded accuracy.

---

## 10. Stage 4d — Identity Verification

**Services**: `DocumentIdentityExtractorService`, `DocumentIdentityValidationPolicy`, `NameMatchingService`

### Identity Extraction

```
rawText (+ graph? when UseIdentityV2=true)
         │
         ├── Extract CNP ──────────────────────────────────────────────
         │
         │   Strategy 1: LabeledCnpRegex
         │     "CNP: 1234567890123" (allows OCR spaces/dots)
         │
         │   Strategy 2: CnpRegex (strict)
         │     \b[1-8]\d{12}\b
         │
         │   Strategy 3: SpaceTolerantCnpRegex
         │     [1-8][\d\s]{12,16} → normalize spaces → validate 13 digits
         │
         │   OCR digit confusion correction (applied before validation):
         │     O→0  o→0  I→1  l→1  |→1  S→5  s→5  B→8
         │
         ├── Extract Name ────────────────────────────────────────────
         │
         │   Strategy 1: LabeledNameRegex (labeled field)
         │     "Nume pacient: Popescu Ion"
         │     "Pacient: ..."
         │     "Prenume: ..."
         │     strips inline fields (Age, CNP, Address on same line)
         │
         │   Strategy 2: Unlabeled candidate lines (UseIdentityV2)
         │     scan lines near known field anchors:
         │     ["data", "sex", "cnp", "telefon", "medic" ...]
         │     filter: alphabetic only, not institution name,
         │             not purely numeric, not a date
         │     pick first passing candidate
         │
         └── DocumentIdentity(ExtractedName, ExtractedCnp,
                              NameConfidence, CnpConfidence)
```

### Identity Validation

```
DocumentIdentity (extracted)  +  User profile (expected: displayName + CNP)
          │
          ▼
DocumentIdentityValidationPolicy.Validate(...)

Validation waterfall (short-circuit on first failure):

  ① MissingName:  extracted.ExtractedName is null/empty
                  → IdentityValidationResult.Failure(MissingName)

  ② MissingCnp:   extracted.ExtractedCnp is null/empty
                  → IdentityValidationResult.Failure(MissingCnp)

  ③ CnpMismatch:  extracted.Cnp.Trim() ≠ expected.Cnp.Trim() (ordinal)
                  → IdentityValidationResult.Failure(CnpMismatch)

  ④ NameMismatch: NameMatchingService.Match(expectedName, extractedName)
                  → IdentityValidationResult.Failure(NameMismatch) if not IsMatch

  ✓ All pass → IdentityValidationResult.Success(...)
               → pipeline continues

  ✗ Any failure → IdentityMismatch set, pipeline aborts, IdentityMismatchDialog shown
```

### Fuzzy Name Matching

```
NameMatchingService.Match(expected, actual)
         │
         ├── Normalize both names:
         │     ToLowerInvariant()
         │     Remove diacritics (NFD decomposition + strip combining marks)
         │
         ├── Exact match after normalization → IsMatch=true, Distance=0
         │
         ├── Sorted token comparison:
         │     sort tokens alphabetically (handles "Ion Popescu" vs "Popescu Ion")
         │     LevenshteinDistance per token ≤ MaxDistancePerToken (2)
         │     → IsMatch=true
         │
         └── Subset matching:
              every expected token has a match in actual (extra middle names OK)
              → IsMatch=true

User-facing error messages by failure reason:
  MissingName  → "This document does not contain a visible patient name..."
  MissingCnp   → "This document does not contain a visible CNP..."
  NameMismatch → "The patient name found in this document does not match your profile."
  CnpMismatch  → "The CNP found in this document does not match your profile."
```

---

## 11. Stage 5 — PII Sanitization

**Service**: `SensitiveDataSanitizer`

Produces a safe preview string for UI display and database storage. The original document
is **never modified**.

```
rawOcrText (all pages joined)
         │
         ▼
Apply regex replacement rules in order:

  Pattern                           Replacement
  ─────────────────────────────────────────────
  \b[1-8]\d{12}\b                   [CNP]
  email regex                       [EMAIL]
  Romanian phone regex              [PHONE]
  Bearer JWT token regex            [TOKEN]
  PNS/CNAS/MED + 6+ digits         [MED-ID]
  12+ digit numeric sequences       [NUM]
  Date patterns (dd.mm.yyyy etc.)   [DATE]
         │
         ▼
truncated to MaxSanitizedPreviewLength (default 2000 chars)
"\n[…truncated for preview]" appended if cut
         │
         ▼
sanitizedPreview (safe for logs, SQLite, and UI)
```

---

## 12. Stage 6 — Persistence & Sync Preparation

**Service**: `OcrSyncPreparationService`  
**Storage**: SQLite via `IOcrDocumentRepository`

```
OcrDocumentSyncRecord
         │
         ▼
OcrDocument (domain entity)
  ├── Id                   Guid
  ├── PatientId            Guid
  ├── OpaqueInternalName   "{documentId:N}"  (no PII in name)
  ├── MimeType             "application/pdf" | "image/jpeg"
  ├── PageCount            int
  ├── Sha256OfNormalized   64-char hex
  ├── SanitizedOcrPreview  ≤2000 chars, PII redacted
  ├── EncryptedVaultPath   opaque path (not synced to cloud)
  ├── ScannedAt            DateTime UTC
  ├── IsDirty = true       → triggers cloud sync later
  └── CreatedAt / UpdatedAt

SQLite write → local store

What IS synced to cloud:        What is NOT synced:
  - document metadata             - raw OCR text
  - sanitized preview             - vault file path
  - sha256                        - master key / DEK
  - page count, mime type         - plaintext document bytes
```

---

## 13. Stage 7 — Medical History Auto-Append

**Service**: `MedicalHistoryAutoAppendService`

```
patientId + sourceDocumentId + sanitizedPreview + docTypeOverride?
         │
         ▼
1. Get patient from repository
2. Check idempotency: GetBySourceDocumentAsync(sourceDocumentId)
   → if entries already exist for this doc → skip (prevent duplicates)
3. MedicalHistoryExtractionService.Extract(sanitizedPreview)
   └── MedicationLineRegex per line:
       name + dosage (mg/g/mcg/ml) + frequency + duration
       → ExtractedHistoryItem[]
4. Classify docType (keyword classifier or override from Stage 4b)
5. Build MedicalHistoryEntry (consolidated for entire document):
   ├── Title:     docType-based title + medication list
   ├── MedicationName, Dosage, Frequency, Duration (joined)
   ├── Notes:     full medication detail or document snippet
   ├── Summary:   ≤120 char condensed summary
   ├── Confidence: avg(extractedItems.Confidence) or 0.5
   └── IsDirty = true
6. AddRangeAsync([consolidatedEntry])
7. Append text block to patient.MedicalHistoryNotes
8. UpdateAsync(patient)

For Prescription documents:
  → additionally call IMedicationApplicationService.AddAsync()
    for each extracted medication (active medication list)
```

### History Entry Title Logic

```
docType        title template
───────────────────────────────────────────────────────────────
Prescription   "Prescription: Metformin 500mg, Aspirin 100mg"
Discharge      "Discharge summary"
LabResult      "Lab results"
Referral       "Referral"
ImagingReport  "Imaging report"
other          "Medical document"
```

---

## 14. ML Audit System

**Service**: `MlPipelineAuditService`  
**Privacy contract**: NO patient text, NO OCR content, NO patient identifiers

```
After each ML inference run:
         │
         ▼
MlAuditRecord (in-memory only, never persisted/synced)
  ├── DocumentId         Guid (no link to patient)
  ├── PredictedType      MedicalDocumentType
  ├── ClassificationConfidence float
  ├── ClassificationMethod  "keyword" | "nl_model" | "feature_print" | ...
  ├── ModelVersion       "v1"
  ├── TokenCount         int (from graph)
  ├── BertUsed           bool
  ├── OcrDuration        TimeSpan
  ├── ClassificationDuration TimeSpan
  ├── ExtractionDuration TimeSpan
  ├── ReviewFlagCount    int
  └── RecordedAt         DateTime UTC

MlPipelineAuditService:
  ├── Circular buffer: max 500 records (oldest dropped when full)
  ├── GetAll() → IReadOnlyList<MlAuditRecord>
  └── GetSummary() → MlPerformanceSummary:
        ├── TotalDocuments
        ├── AverageOcrMs, AverageClassifyMs, AverageExtractMs
        ├── AverageConfidence
        ├── BertUsagePercent
        ├── MethodDistribution { method → count }
        └── TypeDistribution   { docType → count }
```

---

## 15. Feature Flags & Configuration

All pipeline behaviors are controlled by `OcrOptions`:

```
OcrOptions
├── SecurityMode              Strict | RelaxedDebug     default: Strict
├── UseAccurateOcr            bool                      default: true
│     false → VNRecognitionLevel.Fast (lower quality, faster)
│     true  → VNRecognitionLevel.Accurate
├── MaxSanitizedPreviewLength int                       default: 2000
│
│   ── ML feature flags (additive, all off by default) ──────────────
├── UseMlClassification       bool                      default: false
│     false → keyword classifier only, no graph built
│     true  → ClassificationOrchestrator + StructuredDocumentBuilder
│             + OcrDocumentGraph (spatial) built
├── UseMlExtraction           bool                      default: false
│     false → HeuristicFieldExtractor only
│     true  → BertFieldExtractor (if model present) + heuristic merge
├── MlConfidenceThreshold     float                     default: 0.65f
│     NL and FeaturePrint results below this are rejected
│
│   ── Identity extraction flags ────────────────────────────────────
└── UseIdentityV2             bool                      default: false
      false → labeled-field regex only
      true  → + unlabeled candidate line search + ocr graph anchors
              + OCR digit confusion correction
```

### Feature Flag Impact Matrix

```
Flag                  Graph Built?  NER extraction  Table extraction  History append
──────────────────────────────────────────────────────────────────────────────────────
Base (all off)        no            heuristic        no               keyword+heuristic
UseMlClassification   yes           heuristic        yes (LabResult)  docType override
UseMlExtraction       yes           BERT+heuristic   yes (LabResult)  docType override
UseIdentityV2         yes           heuristic        depends          see above
```

---

## 16. Data Models Reference

### Core Pipeline DTOs

```
OcrExtractionResult
  ├── Pages[]: OcrPage[]
  │     └── Blocks[]: OcrTextBlock[]
  │           ├── Text, Confidence
  │           └── Lines[]: OcrLine[]
  ├── RawText: string (computed — all blocks joined)
  ├── OverallStatus: OcrExecutionStatus
  ├── DetectedLanguage: string
  ├── IsRomanianSupported: bool
  └── Graph: OcrDocumentGraph?  (null when ML disabled)

OcrToken  ── atomic unit fed to ML pipeline
  ├── TokenIndex, Text, Confidence
  ├── BoundingBox: OcrBoundingBox (X, Y, Width, Height — normalized [0,1])
  ├── PageIndex, BlockIndex, LineIndex
  └── IsBoundingBoxApproximate: bool

ClassificationResult
  ├── Type: MedicalDocumentType
  ├── Confidence: float [0,1]
  └── Method: string (which layer won)

ExtractedField<T>
  ├── Value: T
  ├── Confidence: float [0,1]
  ├── Method: ExtractionMethod
  └── NeedsReview: bool  (confidence < 0.70f)

ExtractionMethod (enum)
  ├── HeuristicRegex
  ├── MlBertTokenClassifier
  ├── MlNlClassifier
  ├── BoundingBoxAlignment
  └── Combined

StructuredMedicalDocument
  ├── DocumentId, DocumentType
  ├── ClassificationConfidence, ClassificationMethod
  ├── PrimaryExtractionMethod
  ├── PatientName?, PatientId?, DateOfBirth?
  ├── DoctorName?, FacilityName?, Specialty?, DestinationClinic?
  ├── Diagnosis?, Recommendation?, ClinicalNotes?, ReportDate?
  ├── Medications[]: ExtractedMedication[]
  ├── LabResults[]:  ExtractedLabResult[]
  ├── ReviewFlags[]: ReviewFlag[]
  ├── RequiresReview: bool  (any Critical flag)
  └── Metrics: DocumentExtractionMetrics

ReviewFlag
  ├── FieldName: string
  ├── Reason: string
  └── Severity: Critical | Warning | Info

ReviewSeverity (enum)
  ├── Info      → auto-append proceeds normally
  ├── Warning   → auto-append proceeds, marked for review
  └── Critical  → auto-append is blocked

DocumentExtractionMetrics
  ├── TotalTokens: int
  ├── AverageFieldConfidence: float
  ├── OcrDuration, ClassificationDuration, ExtractionDuration: TimeSpan
```

### Field Confidence Reference

| Field | Extractor | Default Confidence |
|---|---|---|
| CNP | HeuristicRegex | 0.92 |
| Date | HeuristicRegex | 0.85 |
| Medication Name | HeuristicRegex | 0.80 |
| Medication Dose | HeuristicRegex | 0.78 |
| Patient Name (labeled) | HeuristicRegex | 0.75 |
| Doctor Name | HeuristicRegex | 0.72 |
| Diagnosis | HeuristicRegex | 0.70 |
| Medication Frequency | HeuristicRegex | 0.68 |
| Medication Duration | HeuristicRegex | 0.65 |
| Lab Analysis Name | BoundingBoxAlignment | 0.85 |
| Lab Result Value | BoundingBoxAlignment | 0.85 |
| Lab Unit | BoundingBoxAlignment | 0.80 |
| Lab Reference Range | BoundingBoxAlignment | 0.80 |
| BERT fields | MlBertTokenClassifier | per-token softmax score |

> `NeedsReview` threshold: **0.70**. Fields below this generate ReviewFlag entries.

---

## 17. Security Architecture

```
┌────────────────────────────────────────────────────────────────────────────┐
│                         SECURITY LAYERS                                     │
│                                                                            │
│  Layer 1: iOS Platform                                                     │
│    ├── NSFileProtectionComplete (quarantine + temp directories)            │
│    ├── iCloud backup exclusion (ExcludeFromBackup)                        │
│    └── LAContext biometric gate (Face ID / Touch ID / Optic ID)           │
│                                                                            │
│  Layer 2: Keychain                                                         │
│    ├── kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly                   │
│    └── 256-bit master key (never leaves the Keychain)                     │
│                                                                            │
│  Layer 3: Cryptographic                                                    │
│    ├── Per-document DEK generated with CSPRNG                             │
│    ├── DEK wrapped with MK using AES-GCM-256                             │
│    ├── Document encrypted with DEK using AES-GCM-256                     │
│    └── 96-bit nonce + 128-bit authentication tag per encryption           │
│                                                                            │
│  Layer 4: PII Separation                                                   │
│    ├── Raw OCR text never reaches logs (SensitiveDataSanitizer)           │
│    ├── Sync records contain only sanitized preview (≤2000 chars)          │
│    ├── ML audit records contain no patient text or identifiers            │
│    └── Vault paths are opaque (documentId:N — no PII in filename)        │
│                                                                            │
│  Layer 5: Identity Verification                                            │
│    ├── CNP exact match (ordinal, after trim)                              │
│    ├── Name fuzzy match (Levenshtein ≤ 2 per token)                      │
│    └── Hard block on identity mismatch (pipeline aborted)                 │
│                                                                            │
│  Layer 6: Cloud Sync Boundary                                              │
│    ├── Plaintext document bytes: NEVER synced                             │
│    ├── Master key: NEVER leaves device                                    │
│    └── Only sanitized metadata and sha256 leave the device                │
└────────────────────────────────────────────────────────────────────────────┘
```

### LoggingRedactionPolicy

All log lines referencing document IDs go through `LoggingRedactionPolicy.SafeDocumentRef(id)`,
which produces a truncated reference safe for log aggregation.

---

## 18. Performance Characteristics

Based on `MlPipelineAuditService` metrics available in `MlPerformanceSummary`.

### Timing Budget (approximate, Accurate OCR, A15+)

```
Stage                           Typical latency
───────────────────────────────────────────────
Normalization (PDF re-render)   50–200 ms
SHA-256 hash                    < 5 ms
AES-GCM encryption              5–20 ms
OCR (1-page, Accurate)          800–2000 ms
OCR (1-page, Fast)              200–500 ms
Keyword classification          < 1 ms (O(1))
NL Text Classifier              10–50 ms
Vision Feature Print            100–300 ms
BERT NER (512 tokens)           200–600 ms
Heuristic extraction            < 5 ms
Geometric table extraction      5–30 ms
Identity verification           < 5 ms
PII sanitization                < 5 ms
SQLite write                    5–20 ms
History append                  10–50 ms
```

### OCR Token Count Impact

```
Tokens       BERT memory    Estimated time
──────────────────────────────────────────
128          ~4 MB          ~100 ms
256          ~8 MB          ~200 ms
512 (max)    ~16 MB         ~400 ms

Sequences longer than 512 tokens are truncated at the [SEP] boundary.
```

### Performance Logging Tags

All stages emit structured log entries with prefix tags for easy filtering:

```
[OCR Flags]    → feature flag state at start
[OCR Perf]     → per-stage timing and token counts
[OCR Perf]     → classification result (DocType, Conf, Method, Ms)
[OCR Identity] → name/CNP extraction and validation
[OCR ML]       → ML disabled notice
[OCR Vault]    → vault init/unlock events
[OCR Sync]     → persistence events
[OCR History]  → auto-append events
[OCR Graph]    → spatial graph token count
[BERT]         → model load, inference, errors
[NL Classifier]→ model load, label, confidence
[FeaturePrint] → vector count, best match distance
[Orchestrator] → fusion decision details
[ML Audit]     → per-record metrics
```

---

## Summary — Full Pipeline in 30 Seconds

```
Document (camera / file / photos)
   │
   ▼  Normalize     strip metadata, fix EXIF orientation
   ▼  Hash          SHA-256 of normalized bytes
   ▼  Encrypt       AES-GCM-256, per-doc DEK, Keychain master key
   ▼  OCR           Apple Vision, on-device, ro-RO primary
   ▼  Classify      keyword → NL model → Feature Print (waterfall)
   ▼  Extract       BERT NER (optional) → heuristic regex → merge
   │               + geometric table extractor for lab results
   ▼  Verify        CNP exact + name fuzzy match vs patient profile
   ▼  Sanitize      redact CNP, phone, email, dates from preview
   ▼  Persist       SQLite OcrDocument (sanitized metadata only)
   ▼  Append        MedicalHistoryEntry + patient notes update
```

Everything runs **on-device**. No OCR data, no raw text, and no cryptographic keys
ever leave the iOS device across the network.
