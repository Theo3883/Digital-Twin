namespace DigitalTwin.Domain.Events;

public record MedicationDeletedEvent(
    Guid PatientId,
    Guid MedicationId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
