using DigitalTwin.OCR.Models.Structured;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// iOS-only: runs a BERT token-classification Core ML model for named-entity extraction.
///
/// Expected model input:  int32[1, seqLen] input_ids, attention_mask, token_type_ids
/// Expected model output: float32[1, seqLen, numLabels] logits
///
/// BIO label set (must match the trained model):
///   O, B-PATIENT_NAME, I-PATIENT_NAME, B-DOCTOR_NAME, I-DOCTOR_NAME,
///   B-DATE, I-DATE, B-DIAGNOSIS, I-DIAGNOSIS, B-MEDICATION, I-MEDICATION,
///   B-CNP, I-CNP, B-FACILITY, I-FACILITY
///
/// When the model is not present (not yet trained / not bundled) the extractor
/// silently returns an empty result and allows heuristic fallback to take over.
/// </summary>
public sealed class BertFieldExtractor
{
    private const string ModelBundleName = "bert_ner_v1.mlmodelc";
    private const int MaxSeqLen = 512;

    private readonly WordPieceTokenizer _tokenizer;
    private readonly ILogger<BertFieldExtractor> _logger;

    // BIO labels in order matching the model's output dimension
    private static readonly string[] Labels =
    [
        "O",
        "B-PATIENT_NAME", "I-PATIENT_NAME",
        "B-DOCTOR_NAME",  "I-DOCTOR_NAME",
        "B-DATE",         "I-DATE",
        "B-DIAGNOSIS",    "I-DIAGNOSIS",
        "B-MEDICATION",   "I-MEDICATION",
        "B-CNP",          "I-CNP",
        "B-FACILITY",     "I-FACILITY",
    ];

    public BertFieldExtractor(
        WordPieceTokenizer tokenizer,
        ILogger<BertFieldExtractor> logger)
    {
        _tokenizer = tokenizer;
        _logger = logger;
    }

    public bool IsModelAvailable()
    {
#if IOS || MACCATALYST
        var bundle = Foundation.NSBundle.MainBundle;
        return bundle.PathForResource(
            System.IO.Path.GetFileNameWithoutExtension(ModelBundleName),
            System.IO.Path.GetExtension(ModelBundleName).TrimStart('.')) is not null;
#else
        return false;
#endif
    }

    public BertExtractionResult Extract(string rawText)
    {
        if (!IsModelAvailable())
            return BertExtractionResult.Unavailable;

#if IOS || MACCATALYST
        return RunInference(rawText);
#else
        return BertExtractionResult.Unavailable;
#endif
    }

#if IOS || MACCATALYST
    private BertExtractionResult RunInference(string rawText)
    {
        try
        {
            var (inputIds, tokenToWord) = _tokenizer.Tokenize(rawText, MaxSeqLen);
            int seqLen = inputIds.Length;

            var bundle = Foundation.NSBundle.MainBundle;
            var modelPath = bundle.PathForResource(
                System.IO.Path.GetFileNameWithoutExtension(ModelBundleName),
                System.IO.Path.GetExtension(ModelBundleName).TrimStart('.'));

            if (modelPath is null) return BertExtractionResult.Unavailable;

            var modelUrl = Foundation.NSUrl.FromFilename(modelPath);
            var model = CoreML.MLModel.Create(modelUrl, out var err);
            if (err is not null || model is null)
            {
                _logger.LogWarning("[BERT] Load error: {Err}", err);
                return BertExtractionResult.Unavailable;
            }

            // Build MLMultiArray inputs
            var shape = new Foundation.NSNumber[] {
                Foundation.NSNumber.FromInt32(1),
                Foundation.NSNumber.FromInt32(seqLen)
            };

            using var inputIdsArr = new CoreML.MLMultiArray(shape, CoreML.MLMultiArrayDataType.Int32, out _);
            using var attentionArr = new CoreML.MLMultiArray(shape, CoreML.MLMultiArrayDataType.Int32, out _);
            using var tokenTypeArr = new CoreML.MLMultiArray(shape, CoreML.MLMultiArrayDataType.Int32, out _);

            for (int i = 0; i < seqLen; i++)
            {
                inputIdsArr[i] = Foundation.NSNumber.FromInt32(inputIds[i]);
                attentionArr[i] = Foundation.NSNumber.FromInt32(1);
                tokenTypeArr[i] = Foundation.NSNumber.FromInt32(0);
            }

            var keys = new[]
            {
                new Foundation.NSString("input_ids"),
                new Foundation.NSString("attention_mask"),
                new Foundation.NSString("token_type_ids"),
            };
            var values = new Foundation.NSObject[]
            {
                inputIdsArr,
                attentionArr,
                tokenTypeArr,
            };
            var inputDict = new Foundation.NSDictionary<Foundation.NSString, Foundation.NSObject>(keys, values);
            var mlInput = new CoreML.MLDictionaryFeatureProvider(inputDict, out _);

            var prediction = model.GetPrediction(mlInput, out var predErr);
            if (predErr is not null || prediction is null)
            {
                _logger.LogWarning("[BERT] Prediction error: {Err}", predErr);
                return BertExtractionResult.Unavailable;
            }

            var logits = prediction.GetFeatureValue("logits")?.MultiArrayValue;
            if (logits is null) return BertExtractionResult.Unavailable;

            // Argmax per position → tag index
            int numLabels = Labels.Length;
            var tags   = new string[seqLen];
            var scores = new float[seqLen];

            for (int pos = 0; pos < seqLen; pos++)
            {
                int bestLabel = 0;
                float bestScore = float.MinValue;
                for (int lbl = 0; lbl < numLabels; lbl++)
                {
                    float v = logits[pos * numLabels + lbl].FloatValue;
                    if (v > bestScore) { bestScore = v; bestLabel = lbl; }
                }
                tags[pos]   = Labels[bestLabel];
                scores[pos] = bestScore;
            }

            var words = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var spans = BioTagAssembler.Assemble(tags, scores, tokenToWord);
            return BuildResult(spans, words);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BERT] Inference exception.");
            return BertExtractionResult.Unavailable;
        }
    }
#endif

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
                case "DOCTOR_NAME"  when doctorName is null:  doctorName  = field; break;
                case "DIAGNOSIS"    when diagnosis is null:   diagnosis   = field; break;
                case "CNP"          when cnp is null:         cnp         = field; break;
                case "MEDICATION":                            medications.Add(field); break;
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

public sealed record BertExtractionResult(
    bool IsAvailable,
    ExtractedField<string>? PatientName,
    ExtractedField<string>? DoctorName,
    ExtractedField<string>? Diagnosis,
    ExtractedField<string>? PatientId,
    IReadOnlyList<ExtractedField<string>> Medications)
{
    public static BertExtractionResult Unavailable =>
        new(false, null, null, null, null, []);
}
