using DigitalTwin.Domain.Events;

namespace DigitalTwin.Domain.Interfaces;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
