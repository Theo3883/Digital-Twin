namespace DigitalTwin.OCR.Models.Enums;

[Flags]
public enum SensitiveDataClass
{
    None = 0,
    Cnp = 1,
    Email = 2,
    Phone = 4,
    MedicalId = 8,
    Token = 16,
    SensitiveDate = 32,
    NumericSequence = 64
}
