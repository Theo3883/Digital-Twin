using DigitalTwin.Domain.Events;

namespace DigitalTwin.Domain.Interfaces;

/// <summary>
/// Dispatches domain events to registered handlers.
/// Application services call this after a domain operation succeeds
/// to trigger side-effects (sync, notifications, etc.) without coupling
/// the domain to infrastructure concerns.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
