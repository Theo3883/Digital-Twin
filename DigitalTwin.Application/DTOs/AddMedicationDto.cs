using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents the data required to add a medication record.
/// </summary>
public class AddMedicationDto
{
    /// <summary>
    /// Gets or sets the medication name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dosage text for the medication.
    /// </summary>
    public string Dosage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the administration frequency.
    /// </summary>
    public string? Frequency { get; set; }

    /// <summary>
    /// Gets or sets the medication administration route.
    /// </summary>
    public MedicationRoute Route { get; set; }

    /// <summary>
    /// Gets or sets the RxCUI identifier when known.
    /// </summary>
    public string? RxCui { get; set; }

    /// <summary>
    /// Gets or sets additional usage instructions.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the clinical reason for taking the medication.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the medication start date.
    /// </summary>
    public DateTime? StartDate { get; set; }
}
