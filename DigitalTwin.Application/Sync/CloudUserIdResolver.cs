using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync;

/// <summary>
/// Resolves local UserId → cloud UserId via email match.
/// Local and cloud use different ID spaces; Patients in cloud store cloud UserId.
/// </summary>
public sealed class CloudUserIdResolver(
    IUserRepository localUser,
    IUserRepository? cloudUser,
    ILogger<CloudUserIdResolver> _) : ICloudUserIdResolver
{
    public async Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId, CancellationToken ct = default)
    {
        if (cloudUser is null) return null;

        var local = await localUser.GetByIdAsync(localUserId);
        if (local is null) return null;

        var cloud = await cloudUser.GetByEmailAsync(local.Email);
        return cloud?.Id;
    }
}
