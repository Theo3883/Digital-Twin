using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync;

/// <summary>
/// Resolves local UserId/PatientId → cloud UserId/PatientId via email match.
/// Local SQLite and cloud PostgreSQL use different ID spaces; this bridge
/// enables drainers to map between them.
/// </summary>
public sealed class CloudIdentityResolver : ICloudIdentityResolver
{
    private readonly IUserRepository _localUser;
    private readonly IUserRepository? _cloudUser;
    private readonly IPatientRepository _localPatient;
    private readonly IPatientRepository? _cloudPatient;

    public CloudIdentityResolver(
        IUserRepository localUser,
        IUserRepository? cloudUser,
        IPatientRepository localPatient,
        IPatientRepository? cloudPatient,
        ILogger<CloudIdentityResolver> logger)
    {
        _localUser    = localUser;
        _cloudUser    = cloudUser;
        _localPatient = localPatient;
        _cloudPatient = cloudPatient;
        _ = logger; // logger reserved for future diagnostic use
    }

    public async Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId, CancellationToken ct = default)
    {
        if (_cloudUser is null) return null;

        var local = await _localUser.GetByIdAsync(localUserId);
        if (local is null) return null;

        var cloud = await _cloudUser.GetByEmailAsync(local.Email);
        return cloud?.Id;
    }

    public async Task<Guid?> ResolveCloudPatientIdAsync(Guid localPatientId, CancellationToken ct = default)
    {
        if (_cloudUser is null || _cloudPatient is null) return null;

        var localPatient = await _localPatient.GetByIdAsync(localPatientId);
        if (localPatient is null) return null;

        var cloudUserId = await ResolveCloudUserIdAsync(localPatient.UserId, ct);
        if (cloudUserId is null) return null;

        var cloudPatient = await _cloudPatient.GetByUserIdAsync(cloudUserId.Value);
        return cloudPatient?.Id;
    }
}
