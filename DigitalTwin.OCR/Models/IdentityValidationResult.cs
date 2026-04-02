namespace DigitalTwin.OCR.Models;

/// <summary>
/// Result of comparing extracted document identity against the patient's profile.
/// </summary>
public sealed record IdentityValidationResult(
    bool IsValid,
    IdentityValidationFailureReason? FailureReason,
    string? ExtractedName,
    string? ExtractedCnp,
    string? ExpectedName,
    string? ExpectedCnp)
{
    public static IdentityValidationResult Success(string extractedName, string extractedCnp,
        string expectedName, string expectedCnp)
        => new(true, null, extractedName, extractedCnp, expectedName, expectedCnp);

    public static IdentityValidationResult Failure(IdentityValidationFailureReason reason,
        string? extractedName, string? extractedCnp, string? expectedName, string? expectedCnp)
        => new(false, reason, extractedName, extractedCnp, expectedName, expectedCnp);

    /// <summary>Returns a user-facing message describing the failure.</summary>
    public string ToUserMessage() => FailureReason switch
    {
        IdentityValidationFailureReason.MissingName =>
            "This document does not contain a visible patient name. Only documents belonging to you can be saved.",
        IdentityValidationFailureReason.MissingCnp =>
            "This document does not contain a visible CNP. Only documents belonging to you can be saved.",
        IdentityValidationFailureReason.NameMismatch =>
            "The patient name found in this document does not match your profile.",
        IdentityValidationFailureReason.CnpMismatch =>
            "The CNP found in this document does not match your profile.",
        _ => "Document identity verification failed."
    };
}

public enum IdentityValidationFailureReason
{
    MissingName,
    MissingCnp,
    CnpMismatch,
    NameMismatch
}
