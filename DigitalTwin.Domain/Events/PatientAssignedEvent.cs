namespace DigitalTwin.Domain.Events;

public record PatientAssignedEvent(
    Guid DoctorId,
    Guid PatientId,
    string PatientEmail) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
