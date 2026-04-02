using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Policies;

/// <summary>
/// Validates that the identity (name + CNP) extracted from a scanned document
/// matches the logged-in patient's profile. Both fields must be present and match.
/// </summary>
public sealed class DocumentIdentityValidationPolicy
{
    private readonly NameMatchingService _nameMatcher;

    public DocumentIdentityValidationPolicy(NameMatchingService nameMatcher)
    {
        _nameMatcher = nameMatcher;
    }

    /// <summary>
    /// Validates the extracted document identity against the expected patient identity.
    /// Returns failure on the first detected issue (missing or mismatch).
    /// </summary>
    public IdentityValidationResult Validate(
        DocumentIdentity extracted,
        string expectedFullName,
        string? expectedCnp)
    {
        // Check presence first
        if (string.IsNullOrWhiteSpace(extracted.ExtractedName))
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.MissingName,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);

        if (string.IsNullOrWhiteSpace(extracted.ExtractedCnp))
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.MissingCnp,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);

        // Check CNP match (exact after trimming)
        if (!string.Equals(extracted.ExtractedCnp.Trim(), expectedCnp?.Trim(), StringComparison.Ordinal))
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.CnpMismatch,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);

        // Check name match (fuzzy)
        var nameResult = _nameMatcher.Match(expectedFullName, extracted.ExtractedName);
        if (!nameResult.IsMatch)
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.NameMismatch,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);

        return IdentityValidationResult.Success(
            extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp!);
    }
}
