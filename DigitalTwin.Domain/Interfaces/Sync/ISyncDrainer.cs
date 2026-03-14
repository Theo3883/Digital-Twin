namespace DigitalTwin.Domain.Interfaces.Sync;

/// <summary>
/// Drains dirty rows for a single entity type between the local SQLite cache
/// and the cloud PostgreSQL database (bidirectional).
///
/// Design contract:
///   • Each implementation handles exactly ONE entity type.
///   • The sync orchestrator discovers all implementations via
///     <see cref="IEnumerable{ISyncDrainer}"/> and calls them ordered by <see cref="Order"/>.
///   • If <see cref="DrainAsync"/> throws, the caller stops the drain cycle so
///     remaining tables are retried on the next cycle (all-or-nothing retry semantics).
///   • Internally each drainer must NOT mark rows as synced before the cloud write
///     succeeds — guaranteeing at-least-once delivery.
/// </summary>
public interface ISyncDrainer
{
    /// <summary>
    /// Execution priority. Lower values run first.
    /// User=0, Patient=1, UserOAuth=2, VitalSign=3, EnvironmentReading=4,
    /// SleepSession=5, Medication=6, DoctorPatientAssignment=7.
    /// </summary>
    int Order { get; }

    string TableName { get; }

    /// <summary>
    /// Pushes dirty local rows to cloud and pulls cloud changes to local.
    /// </summary>
    /// <returns>Total number of rows synced (pushed + pulled), or 0 if nothing to do / cloud not configured.</returns>
    Task<int> DrainAsync(CancellationToken ct = default);
}
