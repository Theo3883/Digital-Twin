using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Infrastructure.Services;

/// <summary>
/// No-op domain event dispatcher. Logs events at Debug level.
/// Wire up real handlers by replacing this registration or decorating it.
/// </summary>
public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly ILogger<NoOpDomainEventDispatcher> _logger;

    public NoOpDomainEventDispatcher(ILogger<NoOpDomainEventDispatcher> logger)
        => _logger = logger;

    public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var e in events)
            _logger.LogDebug("[DomainEvent] {EventType} at {OccurredAt}", e.GetType().Name, e.OccurredAt);
        return Task.CompletedTask;
    }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogDebug("[DomainEvent] {EventType} at {OccurredAt}", domainEvent.GetType().Name, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }
}
