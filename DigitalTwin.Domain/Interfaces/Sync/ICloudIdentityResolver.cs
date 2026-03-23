namespace DigitalTwin.Domain.Interfaces.Sync;

/// <summary>
/// Resolves local identity to cloud identity across the different ID spaces
/// (local SQLite vs cloud PostgreSQL). Identity matching is done by email.
/// </summary>
public interface ICloudIdentityResolver
{
    /// <summary>
    /// Maps a local UserId to the corresponding cloud UserId.
    /// Returns null if the local user or matching cloud user (by email) is not found.
    /// </summary>
    Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId, CancellationToken ct = default);

    /// <summary>
    /// Maps a local PatientId to the corresponding cloud PatientId.
    /// Resolves the chain: local Patient → local User → cloud User (by email) → cloud Patient.
    /// Returns null if any link in the chain is missing.
    /// </summary>
    Task<Guid?> ResolveCloudPatientIdAsync(Guid localPatientId, CancellationToken ct = default);
}
