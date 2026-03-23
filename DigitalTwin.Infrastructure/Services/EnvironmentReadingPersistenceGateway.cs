using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Infrastructure.Services;

/// <summary>
/// Cloud-first / local-fallback persistence gateway for <see cref="EnvironmentReading"/>.
/// Tries to write to the cloud first; on failure, stores locally with IsDirty=true
/// so the drain cycle pushes it in the next sync window.
/// </summary>
public sealed class EnvironmentReadingPersistenceGateway : IPersistenceGateway<EnvironmentReading>
{
    private readonly IEnvironmentReadingRepository  _local;
    private readonly IEnvironmentReadingRepository? _cloud;
    private readonly ILogger<EnvironmentReadingPersistenceGateway> _logger;

    public EnvironmentReadingPersistenceGateway(
        IEnvironmentReadingRepository local,
        IEnvironmentReadingRepository? cloud,
        ILogger<EnvironmentReadingPersistenceGateway> logger)
    {
        _local  = local;
        _cloud  = cloud;
        _logger = logger;
    }

    public async Task PersistAsync(EnvironmentReading entity, CancellationToken ct = default)
    {
        var cloudSucceeded = false;
        if (_cloud is not null)
        {
            try
            {
                await _cloud.AddAsync(entity, markDirty: false);
                cloudSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[EnvSync] Cloud persist failed; will sync later.");
            }
        }

        try
        {
            await _local.AddAsync(entity, markDirty: !cloudSucceeded);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EnvSync] Failed to persist environment reading locally.");
        }
    }
}
