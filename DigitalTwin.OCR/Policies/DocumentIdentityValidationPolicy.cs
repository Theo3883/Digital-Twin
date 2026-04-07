using DigitalTwin.Domain.Services;
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
    private readonly AppDebugLogger<DocumentIdentityValidationPolicy>? _logger;

    public DocumentIdentityValidationPolicy(NameMatchingService nameMatcher)
        : this(nameMatcher, null) { }

    public DocumentIdentityValidationPolicy(
        NameMatchingService nameMatcher,
        AppDebugLogger<DocumentIdentityValidationPolicy>? logger)
    {
        _nameMatcher = nameMatcher;
        _logger = logger;
    }

    private static string MaskCnp(string? cnp)
        => cnp is { Length: >= 4 } ? new string('*', cnp.Length - 4) + cnp[^4..] : cnp ?? "(null)";

    /// <summary>
    /// Validates the extracted document identity against the expected patient identity.
    /// Returns failure on the first detected issue (missing or mismatch).
    /// </summary>
    public IdentityValidationResult Validate(
        DocumentIdentity extracted,
        string expectedFullName,
        string? expectedCnp)
    {
        _logger?.Debug(
            "[OCR Identity] Validating: ExtractedName={ExtName} ExtractedCnp={ExtCnp} ExpectedName={ExpName} ExpectedCnp={ExpCnp}",
            extracted.ExtractedName ?? "(null)",
            MaskCnp(extracted.ExtractedCnp),
            expectedFullName,
            MaskCnp(expectedCnp));
        // Check presence first
        if (string.IsNullOrWhiteSpace(extracted.ExtractedName))
        {
            _logger?.Warn("[OCR Identity] Validation FAILED: MissingName — no patient name found in document.");
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.MissingName,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);
        }

        if (string.IsNullOrWhiteSpace(extracted.ExtractedCnp))
        {
            _logger?.Warn("[OCR Identity] Validation FAILED: MissingCnp — no CNP found in document.");
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.MissingCnp,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);
        }

        // Check CNP match (exact after trimming)
        if (!string.Equals(extracted.ExtractedCnp.Trim(), expectedCnp?.Trim(), StringComparison.Ordinal))
        {
            _logger?.Warn(
                "[OCR Identity] Validation FAILED: CnpMismatch — extracted={ExtCnp} expected={ExpCnp}",
                MaskCnp(extracted.ExtractedCnp), MaskCnp(expectedCnp));
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.CnpMismatch,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);
        }

        // Check name match (fuzzy)
        var nameResult = _nameMatcher.Match(expectedFullName, extracted.ExtractedName);
        if (!nameResult.IsMatch)
        {
            _logger?.Warn(
                "[OCR Identity] Validation FAILED: NameMismatch — extracted={ExtName} expected={ExpName}",
                extracted.ExtractedName, expectedFullName);
            return IdentityValidationResult.Failure(
                IdentityValidationFailureReason.NameMismatch,
                extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp);
        }

        _logger?.Debug("[OCR Identity] Validation PASSED.");
        return IdentityValidationResult.Success(
            extracted.ExtractedName, extracted.ExtractedCnp, expectedFullName, expectedCnp!);
    }
}
