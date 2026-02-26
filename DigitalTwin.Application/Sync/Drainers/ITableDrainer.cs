namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty rows for a single database table from the local SQLite cache to the
/// cloud PostgreSQL database.
///
/// Design contract:
///   • Each implementation knows about exactly ONE entity type.
///   • The sync orchestrator discovers all implementations via
///     <see cref="IEnumerable{ITableDrainer}"/> and calls them in registration order.
///   • If <see cref="DrainAsync"/> throws, the caller MUST stop the drain cycle so
///     that all remaining (and the current) tables are retried on the next cycle.
///     This gives all-or-nothing retry semantics across tables.
///   • Internally each drainer must NOT mark rows as synced before the cloud write
///     succeeds — guaranteeing at-least-once delivery.
///
/// Adding support for a new table = add one new class that implements this interface
/// and register it in DI. Zero changes to existing code (Open/Closed Principle).
/// </summary>
public interface ITableDrainer
{
    /// <summary>Lower values run first. User=0, Patient=1, UserOAuth=2, VitalSign=3, EnvironmentReading=4.</summary>
    int Order { get; }
    string TableName { get; }

    /// <summary>
    /// Loads dirty rows from local SQLite, uploads them to the cloud DB in batches,
    /// then marks them synced and purges old local copies.
    /// </summary>
    /// <returns>Number of rows drained, or 0 if nothing to do / cloud not configured.</returns>
    /// <exception cref="Exception">Any exception propagates up — the caller stops the cycle.</exception>
    Task<int> DrainAsync(CancellationToken ct = default);
}
