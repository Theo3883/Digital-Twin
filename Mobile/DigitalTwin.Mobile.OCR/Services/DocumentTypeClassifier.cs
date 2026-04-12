using DigitalTwin.Mobile.Domain.Interfaces;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Keyword-based document type classifier.
/// </summary>
public sealed class DocumentTypeClassifier : IDocumentTypeClassifier
{
    public string Classify(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return "Unknown";

        var text = ocrText.ToUpperInvariant();

        if (text.Contains("RP.:") || text.Contains("RP:") ||
            text.Contains("REȚETĂ") || text.Contains("RETETA"))
            return "Prescription";

        if (text.Contains("BILET DE TRIMITERE") || text.Contains("MOTIVUL TRIMITERII"))
            return "Referral";

        if (text.Contains("BULETIN DE ANALIZE") ||
            (text.Contains("REZULTAT") && (text.Contains("VALORI DE REFERINȚĂ") || text.Contains("VALORI DE REFERINTA"))))
            return "LabResult";

        if (text.Contains("SCRISOARE MEDICALĂ") || text.Contains("SCRISOARE MEDICALA") ||
            text.Contains("BILET DE IEȘIRE") || text.Contains("BILET DE IESIRE") ||
            text.Contains("EPICRIZĂ") || text.Contains("EPICRIZA"))
            return "Discharge";

        if (text.Contains("CERTIFICAT MEDICAL") || text.Contains("ADEVERINȚĂ MEDICALĂ") ||
            text.Contains("CONCEDIU MEDICAL"))
            return "MedicalCertificate";

        if (text.Contains("ECOGRAFIE") || text.Contains("RADIOGRAFIE") ||
            text.Contains("TOMOGRAFIE") || text.Contains("DESCRIERE IMAGISTICĂ"))
            return "ImagingReport";

        if (text.Contains("ELECTROCARDIOGRAMĂ") || text.Contains("ELECTROCARDIOGRAMA") ||
            (text.Contains("ECG") && (text.Contains("RITM") || text.Contains("FRECVENTA CARDIACA"))))
            return "EcgReport";

        if (text.Contains("PROTOCOL OPERATOR") || text.Contains("INTERVENTIE CHIRURGICALA") ||
            text.Contains("INTERVENȚIE CHIRURGICALĂ"))
            return "OperativeReport";

        if (text.Contains("CONSULTAȚIE DE SPECIALITATE") || text.Contains("CONSULTATIE DE SPECIALITATE") ||
            text.Contains("EXAMEN CLINIC") || text.Contains("EXAMEN OBIECTIV"))
            return "ConsultationNote";

        return "Unknown";
    }
}
