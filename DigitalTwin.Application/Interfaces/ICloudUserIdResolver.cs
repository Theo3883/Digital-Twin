namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Resolves local UserId to cloud UserId by matching users via email.
/// Used by drainers to look up cloud Patient records (which store cloud UserId).
/// </summary>
public interface ICloudUserIdResolver
{
    /// <summary>
    /// Maps a local UserId to the corresponding cloud UserId.
    /// Returns null if the local user or matching cloud user (by email) is not found.
    /// </summary>
    Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId, CancellationToken ct = default);
}
