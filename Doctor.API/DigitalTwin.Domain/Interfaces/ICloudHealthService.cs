namespace DigitalTwin.Domain.Interfaces;

/// <summary>
/// Circuit-breaker for the cloud PostgreSQL database.
///
/// Probes host:port with a short TCP connect (≤1 s) and caches the result:
///   • Available  → result cached for 30 s before the next probe.
///   • Unavailable → result cached for 15 s before retrying.
///
/// All callers that would otherwise block on an Npgsql connection timeout (5 s per
/// connection attempt × N queries) should call <see cref="IsAvailableAsync"/> first
/// and skip the query immediately when it returns <see langword="false"/>.
///
/// Call <see cref="ReportFailure"/> from exception handlers so the circuit trips
/// instantly — without waiting for the next probe cycle.
/// </summary>
public interface ICloudHealthService
{
    /// <summary>
    /// Returns <see langword="true"/> when the cloud DB is reachable.
    /// Returns the cached result immediately when the TTL has not yet expired;
    /// otherwise performs a lightweight TCP probe (≤1 s).
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Marks the cloud as unavailable right now, resetting the retry TTL.
    /// Call this inside any catch block that catches an Npgsql / cloud failure
    /// so subsequent calls skip without probing.
    /// </summary>
    void ReportFailure();
}
