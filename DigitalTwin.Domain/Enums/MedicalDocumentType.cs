namespace DigitalTwin.Domain.Enums;

public enum MedicalDocumentType
{
    Unknown = 0,
    Prescription = 1,           // Rețetă medicală
    Referral = 2,               // Bilet de trimitere
    LabResult = 3,              // Buletin de analize
    Discharge = 4,              // Scrisoare medicală / Epicriză / Bilet de ieșire
    MedicalCertificate = 5,     // Certificat medical / Adeverință medicală
    ImagingReport = 6,          // Ecografie, radiografie, CT, RMN
    EcgReport = 7,              // Electrocardiogramă
    OperativeReport = 8,        // Protocol operator
    ConsultationNote = 9,       // Consultație de specialitate
    GenericClinicForm = 10      // Catch-all for unrecognised clinic forms
}
