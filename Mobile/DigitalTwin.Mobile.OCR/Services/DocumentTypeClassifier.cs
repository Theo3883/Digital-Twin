using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Keyword-based document type classifier.
/// </summary>
public sealed class DocumentTypeClassifier : IDocumentTypeClassifier
{
    private readonly ILogger<DocumentTypeClassifier> _logger;

    public DocumentTypeClassifier(ILogger<DocumentTypeClassifier> logger) => _logger = logger;

    public string Classify(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            _logger.LogDebug("[Classifier] Input text is null/empty → Unknown");
            return "Unknown";
        }

        _logger.LogDebug("[Classifier] Input length={Length} chars", ocrText.Length);

        var text = ocrText.ToUpperInvariant();

        string result;

        if (text.Contains("RP.:") || text.Contains("RP:") ||
            text.Contains("REȚETĂ") || text.Contains("RETETA"))
            result = "Prescription";
        else if (text.Contains("BILET DE TRIMITERE") || text.Contains("MOTIVUL TRIMITERII"))
            result = "Referral";
        else if (text.Contains("BULETIN DE ANALIZE") ||
            (text.Contains("REZULTAT") && (text.Contains("VALORI DE REFERINȚĂ") || text.Contains("VALORI DE REFERINTA"))))
            result = "LabResult";
        else if (text.Contains("SCRISOARE MEDICALĂ") || text.Contains("SCRISOARE MEDICALA") ||
            text.Contains("BILET DE IEȘIRE") || text.Contains("BILET DE IESIRE") ||
            text.Contains("EPICRIZĂ") || text.Contains("EPICRIZA"))
            result = "Discharge";
        else if (text.Contains("CERTIFICAT MEDICAL") || text.Contains("ADEVERINȚĂ MEDICALĂ") ||
            text.Contains("CONCEDIU MEDICAL"))
            result = "MedicalCertificate";
        else if (text.Contains("ECOGRAFIE") || text.Contains("RADIOGRAFIE") ||
            text.Contains("TOMOGRAFIE") || text.Contains("DESCRIERE IMAGISTICĂ"))
            result = "ImagingReport";
        else if (text.Contains("ELECTROCARDIOGRAMĂ") || text.Contains("ELECTROCARDIOGRAMA") ||
            (text.Contains("ECG") && (text.Contains("RITM") || text.Contains("FRECVENTA CARDIACA"))))
            result = "EcgReport";
        else if (text.Contains("PROTOCOL OPERATOR") || text.Contains("INTERVENTIE CHIRURGICALA") ||
            text.Contains("INTERVENȚIE CHIRURGICALĂ"))
            result = "OperativeReport";
        else if (text.Contains("CONSULTAȚIE DE SPECIALITATE") || text.Contains("CONSULTATIE DE SPECIALITATE") ||
            text.Contains("EXAMEN CLINIC") || text.Contains("EXAMEN OBIECTIV"))
            result = "ConsultationNote";
        else
            result = "Unknown";

        _logger.LogInformation("[Classifier] Result={Type} for {Length}-char input", result, ocrText.Length);
        return result;
    }
}
