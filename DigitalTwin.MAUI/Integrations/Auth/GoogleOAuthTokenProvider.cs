using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Platform.Auth;

public class GoogleOAuthTokenProvider : IOAuthTokenProvider
{
    private readonly string _clientId;
    private readonly string _redirectUri;

    public GoogleOAuthTokenProvider(string clientId, string redirectUri)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId), "OAuth Client ID must be configured.");
        _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri), "OAuth Redirect URI must be configured.");
    }

    public async Task<OAuthTokenResult> GetTokensAsync()
    {
#if IOS || MACCATALYST || ANDROID
        var authResult = await WebAuthenticator.Default.AuthenticateAsync(
            new Uri("https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={Uri.EscapeDataString(_clientId)}" +
                    $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                    "&response_type=code" +
                    "&scope=openid%20email%20profile"),
            new Uri(_redirectUri));

        var accessToken = authResult.AccessToken;

        return new OAuthTokenResult
        {
            ProviderUserId = authResult.Properties.GetValueOrDefault("sub") ?? string.Empty,
            Email = authResult.Properties.GetValueOrDefault("email") ?? string.Empty,
            FirstName = authResult.Properties.GetValueOrDefault("given_name") ?? string.Empty,
            LastName = authResult.Properties.GetValueOrDefault("family_name") ?? string.Empty,
            PhotoUrl = authResult.Properties.GetValueOrDefault("picture"),
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("OAuth is only supported on mobile platforms.");
#endif
    }
}
