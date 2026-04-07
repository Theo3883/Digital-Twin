using DigitalTwin.Domain.Enums;
using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class DocumentTypeClassifierTests
{

    [Fact]
    public void Classify_Prescription_WithRp()
    {
        var text = "Rp.: 1. Aspenter 75 mg, dimineata.\n2. Betaloc ZOK 50mg.";
        Assert.Equal(MedicalDocumentType.Prescription, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_Prescription_WithReteta()
    {
        var text = "REȚETĂ MEDICALĂ\nNume pacient: Popescu Ion\n1. Aspenter 75 mg";
        Assert.Equal(MedicalDocumentType.Prescription, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_Referral()
    {
        var text = "BILET DE TRIMITERE\nMotivul trimiterii: control cardiologic\nDiagnostic prezumtiv: HTA";
        Assert.Equal(MedicalDocumentType.Referral, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_LabResult()
    {
        var text = "BULETIN DE ANALIZE\nHemoglobina: 14 g/dl\nLeucocite: 7000";
        Assert.Equal(MedicalDocumentType.LabResult, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_Discharge()
    {
        var text = "SCRISOARE MEDICALĂ\nAnamneza: pacient cu HTA\nDiagnostic: HTA gr II\nRecomandări: Betaloc ZOK 50mg";
        Assert.Equal(MedicalDocumentType.Discharge, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_Unknown_ForRandomText()
    {
        var text = "Aceasta este o notita aleatoare fara cuvinte cheie medicale.";
        Assert.Equal(MedicalDocumentType.Unknown, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_NullOrEmpty_ReturnsUnknown()
    {
        Assert.Equal(MedicalDocumentType.Unknown, DocumentTypeClassifierService.Classify(null));
        Assert.Equal(MedicalDocumentType.Unknown, DocumentTypeClassifierService.Classify(""));
        Assert.Equal(MedicalDocumentType.Unknown, DocumentTypeClassifierService.Classify("   "));
    }

    [Fact]
    public void Classify_MedicalCertificate_WithCertificatMedical()
    {
        var text = "CERTIFICAT MEDICAL\nNumar: 42/2023\nNume: Ionescu Maria\nDiagnostic: Hipertensiune arteriala";
        Assert.Equal(MedicalDocumentType.MedicalCertificate, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_MedicalCertificate_WithConcediuMedical()
    {
        var text = "CONCEDIU MEDICAL\nSerie: OPSNAJ Nr: 12345678\nNume asigurat: Popescu Ion";
        Assert.Equal(MedicalDocumentType.MedicalCertificate, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_ImagingReport_WithEcografie()
    {
        var text = "ECOGRAFIE ABDOMINALA\nPatient: Sandu Teodor\nDescrierea imaginilor: ficat normoecogen.";
        Assert.Equal(MedicalDocumentType.ImagingReport, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_ImagingReport_WithDescriere()
    {
        var text = "DESCRIERE IMAGISTICĂ\nRMN cerebral: structuri normale.";
        Assert.Equal(MedicalDocumentType.ImagingReport, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_EcgReport_WithElectrocardiograma()
    {
        var text = "ELECTROCARDIOGRAMĂ\nRitm sinusal, frecventa cardiaca 72 bpm\nAxa electrica normala.";
        Assert.Equal(MedicalDocumentType.EcgReport, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_EcgReport_WithEcgKeywords()
    {
        var text = "ECG de repaus\nRitm: sinusal regulat\nFrecventa cardiaca: 68 bpm";
        Assert.Equal(MedicalDocumentType.EcgReport, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_OperativeReport_WithProtocolOperator()
    {
        var text = "PROTOCOL OPERATOR\nData interventiei: 15.03.2024\nTip interventie: colecistectomie laparoscopica";
        Assert.Equal(MedicalDocumentType.OperativeReport, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_ConsultationNote_WithConsultatieSpecialitate()
    {
        var text = "CONSULTAȚIE DE SPECIALITATE\nSpecialitate: Cardiologie\nExamen clinic: TA 130/80 mmHg";
        Assert.Equal(MedicalDocumentType.ConsultationNote, DocumentTypeClassifierService.Classify(text));
    }

    [Fact]
    public void Classify_ConsultationNote_WithExamenClinic()
    {
        var text = "Examen clinic\nDiagnostic: angina pectorala stabila\nTratament: Aspirina 100mg";
        Assert.Equal(MedicalDocumentType.ConsultationNote, DocumentTypeClassifierService.Classify(text));
    }
}
