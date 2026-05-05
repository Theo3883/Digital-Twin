namespace DigitalTwin.Mobile.OCR.Models.ML;

/// <summary>
/// Local-only, non-PII audit record for a single ML inference run.
/// Privacy contract: NO patient text, NO OCR content, NO patient identifiers.
/// </summary>
public sealed record MlAuditRecord(
    Guid DocumentId,
    string PredictedType,
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

public sealed record MlPerformanceSummary(
    int TotalDocuments,
    double AverageOcrMs,
    double AverageClassifyMs,
    double AverageExtractMs,
    double AverageConfidence,
    double BertUsagePercent,
    IReadOnlyDictionary<string, int> MethodDistribution,
    IReadOnlyDictionary<string, int> TypeDistribution)
{
    public static MlPerformanceSummary Empty =>
        new(0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(),
            new Dictionary<string, int>());
}
