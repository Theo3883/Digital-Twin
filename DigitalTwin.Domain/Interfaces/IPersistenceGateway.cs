namespace DigitalTwin.Domain.Interfaces;

/// <summary>
/// Strategy interface for cloud-first / local-fallback persistence.
/// Implementations handle the write strategy (try cloud, fall back to marking
/// local records as dirty for the sync drain cycle).
/// This keeps infrastructure persistence concerns out of application services.
/// </summary>
public interface IPersistenceGateway<in T>
{
    Task PersistAsync(T entity, CancellationToken ct = default);
}
