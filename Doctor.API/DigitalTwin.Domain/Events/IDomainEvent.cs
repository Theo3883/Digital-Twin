namespace DigitalTwin.Domain.Events;

/// <summary>
/// Marker interface for all domain events.
/// Events are immutable records that describe something that happened in the domain.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
