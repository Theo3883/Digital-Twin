using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// In-memory local audit log for ML pipeline metrics.
/// No PII, no PHI, no OCR text. Purely for on-device diagnostic analysis.
/// Capped at 500 records; oldest dropped when full.
/// </summary>
public sealed class MlPipelineAuditService
{
    private const int MaxRecords = 500;
    private const string ModelVersion = "v1";

    private readonly List<MlAuditRecord> _records = new(capacity: MaxRecords);
    private readonly ILogger<MlPipelineAuditService> _logger;
    private readonly object _lock = new();

    public MlPipelineAuditService(ILogger<MlPipelineAuditService> logger)
        => _logger = logger;

    public void Record(MlAuditRecord record)
    {
        lock (_lock)
        {
            if (_records.Count >= MaxRecords)
                _records.RemoveAt(0);
            _records.Add(record);
        }

        _logger.LogDebug(
            "[ML Audit] DocId={DocId} Type={Type} Conf={Conf:F3} Method={Method} " +
            "OCR={OcrMs}ms Classify={ClassMs}ms Extract={ExtMs}ms Tokens={Tokens} BERT={Bert} Flags={Flags}",
            record.DocumentId,
            record.PredictedType,
            record.ClassificationConfidence,
            record.ClassificationMethod,
            (int)record.OcrDuration.TotalMilliseconds,
            (int)record.ClassificationDuration.TotalMilliseconds,
            (int)record.ExtractionDuration.TotalMilliseconds,
            record.TokenCount,
            record.BertUsed,
            record.ReviewFlagCount);
    }

    public IReadOnlyList<MlAuditRecord> GetAll()
    {
        lock (_lock)
            return _records.AsReadOnly();
    }

    public MlPerformanceSummary GetSummary()
    {
        IReadOnlyList<MlAuditRecord> records;
        lock (_lock)
            records = _records.ToList();

        if (records.Count == 0)
            return MlPerformanceSummary.Empty;

        return new MlPerformanceSummary(
            TotalDocuments: records.Count,
            AverageOcrMs: records.Average(r => r.OcrDuration.TotalMilliseconds),
            AverageClassifyMs: records.Average(r => r.ClassificationDuration.TotalMilliseconds),
            AverageExtractMs: records.Average(r => r.ExtractionDuration.TotalMilliseconds),
            AverageConfidence: records.Average(r => r.ClassificationConfidence),
            BertUsagePercent: records.Count(r => r.BertUsed) * 100.0 / records.Count,
            MethodDistribution: records
                .GroupBy(r => r.ClassificationMethod)
                .ToDictionary(g => g.Key, g => g.Count()),
            TypeDistribution: records
                .GroupBy(r => r.PredictedType)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()));
    }
}

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
