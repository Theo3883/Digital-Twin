using DigitalTwin.Mobile.OCR.Models.ML;
using DigitalTwin.Mobile.OCR.Models.Structured;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.OCR.Services.ML;

/// <summary>
/// BERT NER field extractor — post-processing only.
/// The actual CoreML inference runs on the Swift side; this class accepts
/// pre-computed logits (float[seqLen, numLabels]) and assembles BIO spans
/// into structured fields.
///
/// When called with raw text on a platform with no model, returns Unavailable.
/// </summary>
public sealed class BertFieldExtractor
{
    private const int MaxSeqLen = 512;

    private readonly WordPieceTokenizer _tokenizer;
    private readonly ILogger<BertFieldExtractor> _logger;
    private readonly string? _vocabPath;

    private static readonly string[] Labels =
    [
        "O",
        "B-PATIENT_NAME", "I-PATIENT_NAME",
        "B-DOCTOR_NAME", "I-DOCTOR_NAME",
        "B-DATE", "I-DATE",
        "B-DIAGNOSIS", "I-DIAGNOSIS",
        "B-MEDICATION", "I-MEDICATION",
        "B-CNP", "I-CNP",
        "B-FACILITY", "I-FACILITY",
    ];

    public BertFieldExtractor(
        WordPieceTokenizer tokenizer,
        ILogger<BertFieldExtractor> logger,
        string? vocabPath = null)
    {
        _tokenizer = tokenizer;
        _logger = logger;
        _vocabPath = vocabPath;
    }

    public bool IsModelAvailable() => _vocabPath is not null && File.Exists(_vocabPath);

    /// <summary>
    /// Extracts fields from raw text given pre-computed logits from Swift CoreML inference.
    /// logits: float array of shape [seqLen * numLabels], row-major.
    /// </summary>
    public BertExtractionResult ExtractFromLogits(string rawText, float[] logits)
    {
        try
        {
            var (inputIds, tokenToWord) = _tokenizer.Tokenize(rawText, MaxSeqLen);
            int seqLen = inputIds.Length;
            int numLabels = Labels.Length;

            var tags = new string[seqLen];
            var scores = new float[seqLen];

            for (int pos = 0; pos < seqLen; pos++)
            {
                int bestLabel = 0;
                float bestScore = float.MinValue;
                for (int lbl = 0; lbl < numLabels; lbl++)
                {
                    float v = logits[pos * numLabels + lbl];
                    if (v > bestScore) { bestScore = v; bestLabel = lbl; }
                }
                tags[pos] = Labels[bestLabel];
                scores[pos] = bestScore;
            }

            var words = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var spans = BioTagAssembler.Assemble(tags, scores, tokenToWord);
            return BuildResult(spans, words);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BERT] Post-processing exception.");
            return BertExtractionResult.Unavailable;
        }
    }

    /// <summary>
    /// Placeholder for direct extraction. In NativeAOT, inference happens on Swift side.
    /// </summary>
    public BertExtractionResult Extract(string rawText)
    {
        _logger.LogDebug("[BERT] Direct extraction not available in NativeAOT — use ExtractFromLogits.");
        return BertExtractionResult.Unavailable;
    }

    private static BertExtractionResult BuildResult(
        IReadOnlyList<BioTagAssembler.EntitySpan> spans, string[] words)
    {
        ExtractedField<string>? patientName = null, doctorName = null, diagnosis = null, cnp = null;
        var medications = new List<ExtractedField<string>>();

        foreach (var span in spans)
        {
            var text = string.Join(" ", words
                .Skip(span.StartWord)
                .Take(span.EndWord - span.StartWord + 1));

            var field = new ExtractedField<string>(text, span.AvgScore, ExtractionMethod.MlBertTokenClassifier);

            switch (span.Label)
            {
                case "PATIENT_NAME" when patientName is null: patientName = field; break;
                case "DOCTOR_NAME" when doctorName is null: doctorName = field; break;
                case "DIAGNOSIS" when diagnosis is null: diagnosis = field; break;
                case "CNP" when cnp is null: cnp = field; break;
                case "MEDICATION": medications.Add(field); break;
            }
        }

        return new BertExtractionResult(
            IsAvailable: true,
            PatientName: patientName,
            DoctorName: doctorName,
            Diagnosis: diagnosis,
            PatientId: cnp,
            Medications: medications);
    }
}
