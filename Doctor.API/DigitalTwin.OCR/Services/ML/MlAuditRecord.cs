using DigitalTwin.Domain.Enums;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// A local-only, non-PII audit record for a single ML inference run.
/// Stored in memory (never persisted or synced) — used only for latency monitoring.
///
/// PRIVACY CONTRACT: this record contains NO patient text, NO OCR content,
/// NO patient identifiers. Only metadata safe for local diagnostic logging.
/// </summary>
public sealed record MlAuditRecord(
    Guid DocumentId,
    MedicalDocumentType PredictedType,
    float ClassificationConfidence,
    string ClassificationMethod,
    string ModelVersion,
    int TokenCount,
    bool BertUsed,
    TimeSpan OcrDuration,
    TimeSpan ClassificationDuration,
    TimeSpan ExtractionDuration,
    int ReviewFlagCount,
    DateTime RecordedAt);
