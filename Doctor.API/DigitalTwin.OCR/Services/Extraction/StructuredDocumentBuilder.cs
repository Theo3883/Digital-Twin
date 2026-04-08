using DigitalTwin.Domain.Enums;
using DigitalTwin.OCR.Models.Graph;
using DigitalTwin.OCR.Models.Structured;
using DigitalTwin.OCR.Services.ML;

namespace DigitalTwin.OCR.Services.Extraction;

/// <summary>
/// Assembles a StructuredMedicalDocument from heuristic or BERT extraction results
/// and geometric table data. BERT results take priority over heuristic when available.
/// </summary>
public sealed class StructuredDocumentBuilder
{
    private readonly HeuristicFieldExtractor _heuristic;
    private readonly GeometricTableExtractor _tableExtractor;
    private readonly BertFieldExtractor? _bert;

    public StructuredDocumentBuilder(
        HeuristicFieldExtractor heuristic,
        GeometricTableExtractor tableExtractor,
        BertFieldExtractor? bert = null)
    {
        _heuristic = heuristic;
        _tableExtractor = tableExtractor;
        _bert = bert;
    }

    public StructuredMedicalDocument Build(
        Guid documentId,
        string rawText,
        MedicalDocumentType docType,
        float classificationConfidence,
        string classificationMethod,
        OcrDocumentGraph? graph,
        TimeSpan ocrDuration,
        TimeSpan classificationDuration,
        bool useMlExtraction = false)
    {
        var extractSw = System.Diagnostics.Stopwatch.StartNew();

        // BERT extraction (Phase 3) — only when enabled and model is available
        BertExtractionResult? bertResult = null;
        if (useMlExtraction && _bert is not null && _bert.IsModelAvailable())
            bertResult = _bert.Extract(rawText);

        // Heuristic extraction — always runs as the primary/fallback extractor
        var heuristic = _heuristic.Extract(rawText, docType);

        // Merge: BERT fields override heuristic when BERT confidence is higher
        var patientName = Merge(bertResult?.PatientName, heuristic.PatientName);
        var patientId   = Merge(bertResult?.PatientId,   heuristic.PatientId);
        var doctorName  = Merge(bertResult?.DoctorName,  heuristic.DoctorName);
        var diagnosis   = Merge(bertResult?.Diagnosis,   heuristic.Diagnosis);
        var reportDate  = heuristic.ReportDate;

        IReadOnlyList<ExtractedMedication> medications;
        if (bertResult is { IsAvailable: true } && bertResult.Medications.Count > 0)
        {
            medications = bertResult.Medications
                .Select(m => new ExtractedMedication(m, null, null, null, null))
                .ToList();
        }
        else
        {
            medications = heuristic.Medications;
        }

        var labResults = (graph is not null && docType == MedicalDocumentType.LabResult)
            ? _tableExtractor.Extract(graph)
            : [];

        extractSw.Stop();

        var allFields = new[]
        {
            patientName, patientId, reportDate, doctorName, diagnosis
        }.OfType<ExtractedField<string>>().ToList();

        var avgConfidence = allFields.Count > 0
            ? allFields.Average(f => f.Confidence)
            : 0f;

        var primaryMethod = (bertResult?.IsAvailable == true)
            ? ExtractionMethod.MlBertTokenClassifier
            : ExtractionMethod.HeuristicRegex;

        var reviewFlags = BuildReviewFlags(patientName, patientId, diagnosis, labResults);

        return new StructuredMedicalDocument
        {
            DocumentId = documentId,
            DocumentType = docType,
            ClassificationConfidence = classificationConfidence,
            ClassificationMethod = classificationMethod,
            PrimaryExtractionMethod = primaryMethod,
            PatientName = patientName,
            PatientId = patientId,
            ReportDate = reportDate,
            DoctorName = doctorName,
            Diagnosis = diagnosis,
            Medications = medications,
            LabResults = labResults,
            ExtractedAt = DateTime.UtcNow,
            ReviewFlags = reviewFlags,
            Metrics = new DocumentExtractionMetrics(
                TotalTokens: graph?.AllTokens.Count ?? 0,
                AverageFieldConfidence: avgConfidence,
                OcrDuration: ocrDuration,
                ClassificationDuration: classificationDuration,
                ExtractionDuration: extractSw.Elapsed)
        };
    }

    /// <summary>
    /// Prefers the primary field if its confidence is higher, otherwise takes the fallback.
    /// </summary>
    private static ExtractedField<string>? Merge(
        ExtractedField<string>? primary,
        ExtractedField<string>? fallback)
    {
        if (primary is null) return fallback;
        if (fallback is null) return primary;
        return primary.Confidence >= fallback.Confidence ? primary : fallback;
    }

    private static IReadOnlyList<ReviewFlag> BuildReviewFlags(
        ExtractedField<string>? patientName,
        ExtractedField<string>? patientId,
        ExtractedField<string>? diagnosis,
        IReadOnlyList<ExtractedLabResult> labResults)
    {
        var flags = new List<ReviewFlag>();

        if (patientName is { NeedsReview: true })
            flags.Add(new ReviewFlag("patient_name", "Low extraction confidence", ReviewSeverity.Critical));

        if (patientId is { NeedsReview: true })
            flags.Add(new ReviewFlag("patient_id", "Low extraction confidence", ReviewSeverity.Critical));

        if (patientName is null)
            flags.Add(new ReviewFlag("patient_name", "Not found in document", ReviewSeverity.Warning));

        if (diagnosis is { NeedsReview: true })
            flags.Add(new ReviewFlag("diagnosis", "Low extraction confidence", ReviewSeverity.Warning));

        foreach (var lab in labResults.Where(l => l.Value.NeedsReview))
            flags.Add(new ReviewFlag("analysis_value", $"Low confidence for {lab.AnalysisName.Value}", ReviewSeverity.Warning));

        return flags;
    }
}
