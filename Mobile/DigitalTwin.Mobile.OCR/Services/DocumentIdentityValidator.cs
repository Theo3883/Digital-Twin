using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Validates extracted identity against the logged-in patient (parity with MAUI
/// <c>DocumentIdentityValidationPolicy</c>): name and CNP must be present on the document,
/// CNP must match exactly, and name must fuzzy-match.
/// </summary>
public sealed class DocumentIdentityValidator : IDocumentIdentityValidator
{
    private readonly INameMatchingService _nameMatcher;

    public DocumentIdentityValidator(INameMatchingService nameMatcher)
    {
        _nameMatcher = nameMatcher;
    }

    public IdentityValidationResult Validate(
        DocumentIdentity identity,
        string? patientName,
        string? patientCnp)
    {
        if (string.IsNullOrWhiteSpace(identity.ExtractedName))
            return new IdentityValidationResult(false, false, false, "No patient name found in document.");

        if (string.IsNullOrWhiteSpace(identity.ExtractedCnp))
            return new IdentityValidationResult(false, false, false, "No CNP found in document.");

        var cnpMatched = !string.IsNullOrEmpty(patientCnp)
                         && string.Equals(identity.ExtractedCnp.Trim(), patientCnp.Trim(), StringComparison.Ordinal);

        if (!cnpMatched)
            return new IdentityValidationResult(false, false, false, "CNP on the document does not match your profile.");

        var nameMatched = false;
        if (!string.IsNullOrEmpty(patientName) && !string.IsNullOrEmpty(identity.ExtractedName))
            nameMatched = _nameMatcher.Match(patientName, identity.ExtractedName).IsMatch;

        if (!nameMatched)
            return new IdentityValidationResult(false, false, true, "Name on the document does not match your profile.");

        return new IdentityValidationResult(true, true, true, null);
    }
}
