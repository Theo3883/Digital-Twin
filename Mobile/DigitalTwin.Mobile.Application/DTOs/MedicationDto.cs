using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Application.DTOs;

public record MedicationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string? Frequency { get; init; }
    public MedicationRoute Route { get; init; }
    public MedicationStatus Status { get; init; }
    public string? RxCui { get; init; }
    public string? Instructions { get; init; }
    public string? Reason { get; init; }
    public Guid? PrescribedByUserId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? DiscontinuedReason { get; init; }
    public AddedByRole AddedByRole { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsSynced { get; init; }
}

public record AddMedicationInput
{
    public string Name { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string? Frequency { get; init; }
    public MedicationRoute Route { get; init; }
    public string? RxCui { get; init; }
    public string? Instructions { get; init; }
    public string? Reason { get; init; }
    public DateTime? StartDate { get; init; }
}

public record DiscontinueMedicationInput
{
    public Guid MedicationId { get; init; }
    public string? Reason { get; init; }
}

public record DrugSearchResultDto
{
    public string Name { get; init; } = string.Empty;
    public string RxCui { get; init; } = string.Empty;
}

public record MedicationInteractionDto
{
    public string DrugARxCui { get; init; } = string.Empty;
    public string DrugBRxCui { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
