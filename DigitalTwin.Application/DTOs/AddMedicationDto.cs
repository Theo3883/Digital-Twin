using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.DTOs;

public class AddMedicationDto
{
    public string Name { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string? Frequency { get; set; }
    public MedicationRoute Route { get; set; }
    public string? RxCui { get; set; }
    public string? Instructions { get; set; }
    public string? Reason { get; set; }
    public DateTime? StartDate { get; set; }
}
