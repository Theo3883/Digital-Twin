using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

public class MedicationInteractionDto
{
    public string DrugARxCui { get; set; } = string.Empty;
    public string DrugBRxCui { get; set; } = string.Empty;
    public InteractionSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}
