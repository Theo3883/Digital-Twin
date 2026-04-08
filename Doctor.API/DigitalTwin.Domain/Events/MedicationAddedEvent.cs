namespace DigitalTwin.Domain.Events;

public record MedicationAddedEvent(
    Guid PatientId,
    Guid MedicationId,
    string Name) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
