namespace DigitalTwin.Mobile.OCR.Policies;

/// <summary>
/// Defines which data categories must never appear in logs.
/// </summary>
public static class LoggingRedactionPolicy
{
    public static readonly IReadOnlySet<string> RedactedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ocrtext", "rawtext", "sanitizedpreview", "documentname", "filename",
        "vaultpath", "encryptedvaultpath", "sha256", "dek", "masterkey",
        "accesstoken", "idtoken", "refreshtoken", "bearertoken",
        "cnp", "email", "phone", "patientname"
    };

    public static bool ShouldRedactPropertyName(string propertyName)
        => RedactedFields.Contains(propertyName);

    public static string SafeDocumentRef(Guid documentId)
        => $"doc:{documentId:N}";

    public static string SafePatientRef(Guid patientId)
        => $"patient:{patientId:N}";
}
