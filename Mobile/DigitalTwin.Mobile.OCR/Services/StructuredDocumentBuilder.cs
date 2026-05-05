using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.OCR.Models.Graph;
using DigitalTwin.Mobile.OCR.Models.ML;
using DigitalTwin.Mobile.OCR.Models.Structured;
using DigitalTwin.Mobile.OCR.Services.ML;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Assembles a StructuredMedicalDocument from heuristic or BERT extraction results
/// and geometric table data. BERT results take priority over heuristic when available.
/// </summary>
public sealed class StructuredDocumentBuilder
{
    private readonly IHeuristicFieldExtractor _heuristic;
    private readonly GeometricTableExtractor _tableExtractor;
    private readonly BertFieldExtractor? _bert;

    public StructuredDocumentBuilder(
        IHeuristicFieldExtractor heuristic,
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
        string docType,
        float classificationConfidence,
        string classificationMethod,
        OcrDocumentGraph? graph,
        TimeSpan ocrDuration,
        TimeSpan classificationDuration,
        bool useMlExtraction = false)
    {
        var extractSw = System.Diagnostics.Stopwatch.StartNew();

        BertExtractionResult? bertResult = null;
        if (useMlExtraction && _bert is not null && _bert.IsModelAvailable())
            bertResult = _bert.Extract(rawText);

        var heuristic = _heuristic.Extract(rawText, docType);

        var patientName = Merge(bertResult?.PatientName, ToField(heuristic.PatientName));
        var patientId = Merge(bertResult?.PatientId, ToField(heuristic.PatientId));
        var doctorName = Merge(bertResult?.DoctorName, ToField(heuristic.DoctorName));
        var diagnosis = Merge(bertResult?.Diagnosis, ToField(heuristic.Diagnosis));
        var reportDate = ToField(heuristic.ReportDate);

        IReadOnlyList<ExtractedMedication> medications;
        if (bertResult is { IsAvailable: true } && bertResult.Medications.Count > 0)
        {
            medications = bertResult.Medications
                .Select(m => new ExtractedMedication(m, null, null, null, null))
                .ToList();
        }
        else
        {
            medications = heuristic.Medications
                .Select(m => new ExtractedMedication(
                    new ExtractedField<string>(m.Name, 0.75f, ExtractionMethod.HeuristicRegex),
                    m.Dosage is not null ? new ExtractedField<string>(m.Dosage, 0.70f, ExtractionMethod.HeuristicRegex) : null,
                    m.Frequency is not null ? new ExtractedField<string>(m.Frequency, 0.65f, ExtractionMethod.HeuristicRegex) : null,
                    null, null))
                .ToList();
        }

        var labResults = (graph is not null && docType == "LabResult")
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

    private static ExtractedField<string>? ToField(string? value) =>
        value is null ? null : new ExtractedField<string>(value, 0.75f, ExtractionMethod.HeuristicRegex);

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
