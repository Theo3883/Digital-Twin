using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Application.DTOs;

public sealed record SaveOcrDocumentInput
{
    /// <summary>Vault / SQLite document id (same Guid used for encrypted blob). If empty, a new id is generated.</summary>
    public string? DocumentId { get; init; }

    public string OpaqueInternalName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public string[] PageTexts { get; init; } = [];

    public string? EncryptedVaultPath { get; init; }
    public string? Sha256OfNormalized { get; init; }
    public string? VaultOpaqueInternalName { get; init; }
    public string? DocumentTypeOverride { get; init; }

    /// <summary>Round-trip JSON from <c>ProcessFullOcr</c> (preferred for Swift ↔ NativeAOT).</summary>
    public string? CachedProcessingResultJson { get; init; }

    public OcrTextProcessingResult? CachedProcessingResult { get; init; }
}

public sealed record BuildStructuredDocumentInput
{
    public string DocumentId { get; init; } = string.Empty;
    public string OcrText { get; init; } = string.Empty;
    public string DocType { get; init; } = "Unknown";
    public float ClassConfidence { get; init; }
    public string ClassMethod { get; init; } = string.Empty;
    public bool UseMlExtraction { get; init; } = true;
    public long OcrDurationMs { get; init; }
    public long ClassificationDurationMs { get; init; }
}
