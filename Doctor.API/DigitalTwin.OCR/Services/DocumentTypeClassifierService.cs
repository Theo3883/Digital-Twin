using DigitalTwin.Domain.Enums;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Keyword-based classifier that determines the type of a scanned medical document
/// from its OCR text content.
/// </summary>
public sealed class DocumentTypeClassifierService
{
    public static MedicalDocumentType Classify(string? ocrText)
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

        // Medical certificate: "CERTIFICAT MEDICAL" / "ADEVERINȚĂ MEDICALĂ" / "CONCEDIU MEDICAL"
        if (text.Contains("CERTIFICAT MEDICAL") || text.Contains("ADEVERINȚĂ MEDICALĂ") ||
            text.Contains("ADEVERINTA MEDICALA") || text.Contains("CONCEDIU MEDICAL"))
            return MedicalDocumentType.MedicalCertificate;

        // Imaging report: "ECOGRAFIE" / "RADIOGRAFIE" / "TOMOGRAFIE" / "RMN" / "DESCRIERE IMAGISTICĂ"
        if (text.Contains("ECOGRAFIE") || text.Contains("RADIOGRAFIE") ||
            text.Contains("TOMOGRAFIE") || text.Contains("DESCRIERE IMAGISTICĂ") ||
            text.Contains("DESCRIERE IMAGISTICA") || text.Contains("EXAMEN RMN"))
            return MedicalDocumentType.ImagingReport;

        // ECG report: "ELECTROCARDIOGRAMĂ" / "ECG" + cardiac keywords
        if (text.Contains("ELECTROCARDIOGRAMĂ") || text.Contains("ELECTROCARDIOGRAMA") ||
            (text.Contains("ECG") && (text.Contains("RITM") || text.Contains("FRECVENTA CARDIACA"))))
            return MedicalDocumentType.EcgReport;

        // Operative report: "PROTOCOL OPERATOR" / "INTERVENȚIE CHIRURGICALĂ"
        if (text.Contains("PROTOCOL OPERATOR") || text.Contains("INTERVENTIE CHIRURGICALA") ||
            text.Contains("INTERVENȚIE CHIRURGICALĂ") || text.Contains("PROCEDURA CHIRURGICALA"))
            return MedicalDocumentType.OperativeReport;

        // Consultation note: "CONSULTAȚIE" / "EXAMEN CLINIC" / "EXAMEN OBIECTIV"
        if (text.Contains("CONSULTAȚIE DE SPECIALITATE") || text.Contains("CONSULTATIE DE SPECIALITATE") ||
            text.Contains("EXAMEN CLINIC") || text.Contains("EXAMEN OBIECTIV") ||
            (text.Contains("CONSULTAȚIE") && text.Contains("DIAGNOSTIC")))
            return MedicalDocumentType.ConsultationNote;

        return MedicalDocumentType.Unknown;
    }
}
