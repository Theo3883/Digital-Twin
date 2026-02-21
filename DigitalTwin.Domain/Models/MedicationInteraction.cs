using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

public class MedicationInteraction
{
    public string DrugARxCui { get; set; } = string.Empty;
    public string DrugBRxCui { get; set; } = string.Empty;
    public InteractionSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
}
