using System.Text.Json;
using DigitalTwin.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// iOS-only: visual document classifier using VNGenerateImageFeaturePrintRequest.
///
/// Loads reference feature vectors from reference_feature_prints.json (bundled in app)
/// and finds the nearest reference using cosine distance. Zero training data required.
///
/// iOS 17+: uses 768-float Revision 2 vectors.
/// iOS 15/16: falls back to 2048-float Scene Feature Print (Revision 1).
/// </summary>
public sealed class FeaturePrintDocumentClassifier : IDocumentTypeClassifier
{
    private const string ReferenceJsonBundleName = "reference_feature_prints";
    private const float MaxCosineDistance = 0.25f;
    private readonly ILogger<FeaturePrintDocumentClassifier> _logger;

    public FeaturePrintDocumentClassifier(ILogger<FeaturePrintDocumentClassifier> logger)
        => _logger = logger;

#if IOS || MACCATALYST
    private record ReferenceEntry(string Label, float[] Vector);
    private IReadOnlyList<ReferenceEntry>? _references;
    private bool _loadAttempted;

    private IReadOnlyList<ReferenceEntry> LoadReferences()
    {
        if (_loadAttempted) return _references ?? [];
        _loadAttempted = true;

        try
        {
            var bundle = Foundation.NSBundle.MainBundle;
            var jsonPath = bundle.PathForResource(ReferenceJsonBundleName, "json", "Models");
            if (jsonPath is null)
            {
                _logger.LogWarning("[FeaturePrint] reference_feature_prints.json not found in bundle.");
                return [];
            }

            var json = System.IO.File.ReadAllText(jsonPath);
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            if (entries is null) return [];

            _references = entries
                .Select(e => new ReferenceEntry(
                    e.GetProperty("label").GetString()!,
                    e.GetProperty("vector").EnumerateArray()
                        .Select(v => v.GetSingle()).ToArray()))
                .ToList();

            _logger.LogDebug("[FeaturePrint] Loaded {Count} reference vectors.", _references.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FeaturePrint] Failed to load reference vectors.");
            _references = [];
        }

        return _references;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        string ocrText, string? imagePath, CancellationToken ct = default)
    {
        if (imagePath is null)
            return Unknown("no_image_path");

        var references = LoadReferences();
        if (references.Count == 0)
            return Unknown("no_reference_vectors");

        try
        {
            var queryVector = await ComputeFeaturePrintAsync(imagePath, ct);
            if (queryVector is null)
                return Unknown("feature_print_failed");

            var best = references
                .Select(r => (r.Label, Distance: CosineDistance(queryVector, r.Vector)))
                .OrderBy(x => x.Distance)
                .First();

            _logger.LogDebug("[FeaturePrint] Best={Label} Dist={Dist:F4}", best.Label, best.Distance);

            if (best.Distance > MaxCosineDistance)
                return Unknown("distance_too_large");

            var confidence = 1f - best.Distance / MaxCosineDistance;
            var docType = ParseLabel(best.Label);
            return new ClassificationResult(docType, confidence, "feature_print");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FeaturePrint] Inference error.");
            return Unknown("feature_print_error");
        }
    }

    private static async Task<float[]?> ComputeFeaturePrintAsync(
        string imagePath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var url = Foundation.NSUrl.FromFilename(imagePath);
            var source = ImageIO.CGImageSource.FromUrl(url, (ImageIO.CGImageOptions?)null);
            if (source is null) return null;

            using var cgImage = source.CreateImage(0, (ImageIO.CGImageOptions?)null);
            if (cgImage is null) return null;

            var tcs = new TaskCompletionSource<float[]?>();

            var request = new Vision.VNGenerateImageFeaturePrintRequest((req, err) =>
            {
                if (err is not null)
                {
                    tcs.TrySetResult(null);
                    return;
                }
                var obs = (req as Vision.VNGenerateImageFeaturePrintRequest)
                    ?.Results?.OfType<Vision.VNFeaturePrintObservation>().FirstOrDefault();
                if (obs is null) { tcs.TrySetResult(null); return; }

                var count = (int)obs.ElementCount;
                var bytes = obs.Data?.ToArray();
                if (bytes is null || bytes.Length < count * sizeof(float))
                {
                    tcs.TrySetResult(null);
                    return;
                }

                var arr = new float[count];
                Buffer.BlockCopy(bytes, 0, arr, 0, count * sizeof(float));
                tcs.TrySetResult(arr);
            });

            var handler = new Vision.VNImageRequestHandler(cgImage, new Foundation.NSDictionary());
            handler.Perform([request], out _);

            return tcs.Task.IsCompletedSuccessfully ? tcs.Task.Result : null;
        }, ct);
    }
#else
    public Task<ClassificationResult> ClassifyAsync(
        string ocrText, string? imagePath, CancellationToken ct = default)
        => Task.FromResult(Unknown("platform_not_supported"));
#endif

    private static float CosineDistance(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < len; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        if (na == 0 || nb == 0) return 1f;
        return (float)(1.0 - dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }

    private static ClassificationResult Unknown(string method)
        => new(MedicalDocumentType.Unknown, 0f, method);

    private static MedicalDocumentType ParseLabel(string label) => label switch
    {
        "Prescription"       => MedicalDocumentType.Prescription,
        "Referral"           => MedicalDocumentType.Referral,
        "LabResult"          => MedicalDocumentType.LabResult,
        "Discharge"          => MedicalDocumentType.Discharge,
        "MedicalCertificate" => MedicalDocumentType.MedicalCertificate,
        "ImagingReport"      => MedicalDocumentType.ImagingReport,
        "EcgReport"          => MedicalDocumentType.EcgReport,
        "OperativeReport"    => MedicalDocumentType.OperativeReport,
        "ConsultationNote"   => MedicalDocumentType.ConsultationNote,
        "GenericClinicForm"  => MedicalDocumentType.GenericClinicForm,
        _                    => MedicalDocumentType.Unknown
    };
}
