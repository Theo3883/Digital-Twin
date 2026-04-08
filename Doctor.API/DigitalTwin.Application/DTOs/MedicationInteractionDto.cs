using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a detected interaction between two medications.
/// </summary>
public class MedicationInteractionDto
{
    /// <summary>
    /// Gets or sets the RxCUI identifier for the first drug.
    /// </summary>
    public string DrugARxCui { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RxCUI identifier for the second drug.
    /// </summary>
    public string DrugBRxCui { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the interaction severity.
    /// </summary>
    public InteractionSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the interaction description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
