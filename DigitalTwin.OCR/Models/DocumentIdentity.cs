namespace DigitalTwin.OCR.Models;

/// <summary>
/// Identity fields extracted from a scanned document's raw OCR text.
/// Used to verify that the document belongs to the logged-in patient.
/// </summary>
public sealed record DocumentIdentity(
    string? ExtractedName,
    string? ExtractedCnp,
    float NameConfidence,
    float CnpConfidence);
