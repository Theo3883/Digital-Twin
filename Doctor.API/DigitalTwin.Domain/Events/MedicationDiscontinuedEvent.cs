namespace DigitalTwin.Domain.Events;

public record MedicationDiscontinuedEvent(
    Guid PatientId,
    Guid MedicationId,
    string? Reason) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
