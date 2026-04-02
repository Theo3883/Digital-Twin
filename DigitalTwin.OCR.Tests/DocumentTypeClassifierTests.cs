using DigitalTwin.Domain.Enums;
using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class DocumentTypeClassifierTests
{
    private readonly DocumentTypeClassifierService _svc = new();

    [Fact]
    public void Classify_Prescription_WithRp()
    {
        var text = "Rp.: 1. Aspenter 75 mg, dimineata.\n2. Betaloc ZOK 50mg.";
        Assert.Equal(MedicalDocumentType.Prescription, _svc.Classify(text));
    }

    [Fact]
    public void Classify_Prescription_WithReteta()
    {
        var text = "REȚETĂ MEDICALĂ\nNume pacient: Popescu Ion\n1. Aspenter 75 mg";
        Assert.Equal(MedicalDocumentType.Prescription, _svc.Classify(text));
    }

    [Fact]
    public void Classify_Referral()
    {
        var text = "BILET DE TRIMITERE\nMotivul trimiterii: control cardiologic\nDiagnostic prezumtiv: HTA";
        Assert.Equal(MedicalDocumentType.Referral, _svc.Classify(text));
    }

    [Fact]
    public void Classify_LabResult()
    {
        var text = "BULETIN DE ANALIZE\nHemoglobina: 14 g/dl\nLeucocite: 7000";
        Assert.Equal(MedicalDocumentType.LabResult, _svc.Classify(text));
    }

    [Fact]
    public void Classify_Discharge()
    {
        var text = "SCRISOARE MEDICALĂ\nAnamneza: pacient cu HTA\nDiagnostic: HTA gr II\nRecomandări: Betaloc ZOK 50mg";
        Assert.Equal(MedicalDocumentType.Discharge, _svc.Classify(text));
    }

    [Fact]
    public void Classify_Unknown_ForRandomText()
    {
        var text = "Aceasta este o notita aleatoare fara cuvinte cheie medicale.";
        Assert.Equal(MedicalDocumentType.Unknown, _svc.Classify(text));
    }

    [Fact]
    public void Classify_NullOrEmpty_ReturnsUnknown()
    {
        Assert.Equal(MedicalDocumentType.Unknown, _svc.Classify(null));
        Assert.Equal(MedicalDocumentType.Unknown, _svc.Classify(""));
        Assert.Equal(MedicalDocumentType.Unknown, _svc.Classify("   "));
    }
}
