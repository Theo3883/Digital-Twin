namespace DigitalTwin.Domain.Events;

public record PatientUnassignedEvent(
    Guid DoctorId,
    Guid PatientId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
