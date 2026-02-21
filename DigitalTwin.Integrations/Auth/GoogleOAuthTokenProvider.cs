using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Integrations.Auth;

/// <summary>
/// Platform-specific OAuth provider. Requires MAUI WebAuthenticator at runtime.
/// Registered conditionally in the MAUI project.
/// </summary>
public class GoogleOAuthTokenProvider : IOAuthTokenProvider
{
    public Task<OAuthTokenResult> GetTokensAsync()
    {
        throw new PlatformNotSupportedException(
            "GoogleOAuthTokenProvider must be replaced by the platform-specific " +
            "implementation registered in MauiProgram.cs using WebAuthenticator.");
    }
}
