using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a medication record exposed by the application layer.
/// </summary>
public class MedicationDto
{
    /// <summary>
    /// Gets or sets the medication identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the medication name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dosage text.
    /// </summary>
    public string Dosage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the administration frequency.
    /// </summary>
    public string? Frequency { get; set; }

    /// <summary>
    /// Gets or sets the medication route.
    /// </summary>
    public MedicationRoute Route { get; set; }

    /// <summary>
    /// Gets or sets the current medication status.
    /// </summary>
    public MedicationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the RxCUI identifier.
    /// </summary>
    public string? RxCui { get; set; }

    /// <summary>
    /// Gets or sets additional medication instructions.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the treatment reason.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the prescribing user identifier when the medication was prescribed by a user.
    /// </summary>
    public Guid? PrescribedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the medication start date.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the medication end date.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the reason the medication was discontinued.
    /// </summary>
    public string? DiscontinuedReason { get; set; }

    /// <summary>
    /// Gets or sets who added the medication.
    /// </summary>
    public AddedByRole AddedByRole { get; set; }

    /// <summary>
    /// Gets or sets when the medication record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
