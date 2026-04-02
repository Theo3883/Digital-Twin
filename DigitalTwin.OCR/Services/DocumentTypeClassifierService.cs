using DigitalTwin.Domain.Enums;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Keyword-based classifier that determines the type of a scanned medical document
/// from its OCR text content.
/// </summary>
public sealed class DocumentTypeClassifierService
{
    public MedicalDocumentType Classify(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return MedicalDocumentType.Unknown;

        var text = ocrText.ToUpperInvariant();

        // Prescription: "Rp.:" / "Rp:" / "REȚETĂ MEDICALĂ" / "RETETA MEDICALA"
        if (text.Contains("RP.:") || text.Contains("RP:") ||
            text.Contains("REȚETĂ MEDICALĂ") || text.Contains("RETETA MEDICALA") ||
            text.Contains("REȚETĂ") || text.Contains("RETETA"))
            return MedicalDocumentType.Prescription;

        // Referral: "BILET DE TRIMITERE" / "MOTIVUL TRIMITERII" / "DIAGNOSTIC PREZUMTIV"
        if (text.Contains("BILET DE TRIMITERE") || text.Contains("MOTIVUL TRIMITERII") ||
            text.Contains("DIAGNOSTIC PREZUMTIV"))
            return MedicalDocumentType.Referral;

        // Lab result: "BULETIN DE ANALIZE" or presence of results table markers
        if (text.Contains("BULETIN DE ANALIZE") ||
            (text.Contains("REZULTAT") && text.Contains("VALORI DE REFERINȚĂ")) ||
            (text.Contains("REZULTAT") && text.Contains("VALORI DE REFERINTA")))
            return MedicalDocumentType.LabResult;

        // Discharge: "SCRISOARE MEDICALĂ" / "BILET DE IEȘIRE" / "EPICRIZĂ"
        if (text.Contains("SCRISOARE MEDICALĂ") || text.Contains("SCRISOARE MEDICALA") ||
            text.Contains("BILET DE IEȘIRE") || text.Contains("BILET DE IESIRE") ||
            text.Contains("EPICRIZĂ") || text.Contains("EPICRIZA"))
            return MedicalDocumentType.Discharge;

        return MedicalDocumentType.Unknown;
    }
}
