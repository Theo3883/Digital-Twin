using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Infrastructure.Services;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider provider, ILogger<DomainEventDispatcher> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
            await DispatchAsync(domainEvent, ct);
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = _provider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                var task = (Task?)handlerType
                    .GetMethod("HandleAsync")
                    ?.Invoke(handler, new object?[] { domainEvent, ct });

                if (task is not null)
                    await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DomainEventDispatcher] Handler failed for {EventType}", domainEvent.GetType().Name);
            }
        }
    }
}
