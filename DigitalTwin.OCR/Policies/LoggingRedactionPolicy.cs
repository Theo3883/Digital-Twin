namespace DigitalTwin.OCR.Policies;

/// <summary>
/// Defines which data categories must never appear in logs.
/// Used by SensitiveDataSanitizer and the redacting logger.
/// </summary>
public static class LoggingRedactionPolicy
{
    /// <summary>Fields that must always be redacted in log output.</summary>
    public static readonly IReadOnlySet<string> RedactedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ocrtext", "rawtext", "sanitizedpreview", "documentname", "filename",
        "vaultpath", "encryptedvaultpath", "sha256", "dek", "masterkey",
        "accesstoken", "idtoken", "refreshtoken", "bearertoken",
        "cnp", "email", "phone", "patientname"
    };

    public static bool ShouldRedactPropertyName(string propertyName)
        => RedactedFields.Contains(propertyName);

    /// <summary>Safe opaque document reference for logs — never uses real names.</summary>
    public static string SafeDocumentRef(Guid documentId)
        => $"doc:{documentId:N}";

    /// <summary>Safe patient reference — never logs PII.</summary>
    public static string SafePatientRef(Guid patientId)
        => $"patient:{patientId:N}";
}
