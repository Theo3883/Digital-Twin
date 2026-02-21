namespace DigitalTwin.Domain.Models;

public class Medication
{
    public string Name { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? RxCui { get; set; }
    public DateTime PrescribedDate { get; set; }
}
