using System.Text.Json;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Integrations.Auth;

/// <summary>
/// Google OAuth 2.0 / OpenID Connect provider using MAUI <c>WebAuthenticator</c>.
/// Implements the authorization-code flow for mobile (no client secret — public client PKCE).
/// id_token is verified server-side via Google's tokeninfo endpoint (OWASP A02).
/// </summary>
public class GoogleOAuthTokenProvider : IOAuthTokenProvider
{
    private const string GoogleTokenInfoEndpoint = "https://oauth2.googleapis.com/tokeninfo";

    private readonly string _clientId;
    private readonly ILogger<GoogleOAuthTokenProvider> _logger;

#if IOS || MACCATALYST
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private readonly string _redirectUri;
    private readonly IHttpClientFactory _httpClientFactory;
#endif

    public GoogleOAuthTokenProvider(
        string clientId,
        string redirectUri,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleOAuthTokenProvider> logger)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _logger = logger;
#if IOS || MACCATALYST
        _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
#endif
    }

    public async Task<OAuthTokenResult> GetTokensAsync()
    {
#if IOS || MACCATALYST
        _logger.LogInformation("[OAuth] Starting Google OAuth.");

        var authUrl = GoogleAuthEndpoint +
            $"?client_id={Uri.EscapeDataString(_clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
            "&response_type=code" +
            "&scope=openid%20email%20profile" +
            "&access_type=offline" +
            "&prompt=consent";

        var authResult = await WebAuthenticator.Default.AuthenticateAsync(
            new Uri(authUrl), new Uri(_redirectUri));

        _logger.LogDebug("[OAuth] Browser callback received. PropertyCount={Count}", authResult.Properties.Count);

        var code = authResult.Properties.GetValueOrDefault("code");
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogError("[OAuth] No authorization code in callback.");
            throw new InvalidOperationException("Google OAuth did not return an authorization code.");
        }

        _logger.LogDebug("[OAuth] Authorization code received. Exchanging for tokens...");

        using var http = _httpClientFactory.CreateClient("GoogleOAuth");
        var tokenResponse = await http.PostAsync(GoogleTokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _clientId,
                ["redirect_uri"] = _redirectUri,
                ["grant_type"] = "authorization_code"
            }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("[OAuth] Token exchange failed. Status={Status}", tokenResponse.StatusCode);
            tokenResponse.EnsureSuccessStatusCode();
        }

        var responseBody = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<GoogleTokenResponse>(responseBody);
        if (tokenData is null)
        {
            _logger.LogError("[OAuth] Failed to deserialize token response.");
            throw new InvalidOperationException("Failed to parse Google token response.");
        }

        _logger.LogDebug("[OAuth] Token exchange success. HasAccess={HasAccess}, HasRefresh={HasRefresh}, HasId={HasId}, ExpiresIn={Exp}",
            tokenData.AccessToken is not null, tokenData.RefreshToken is not null,
            tokenData.IdToken is not null, tokenData.ExpiresIn);

        var claims = await VerifyIdTokenAsync(tokenData.IdToken, http);
        _logger.LogDebug("[OAuth] id_token signature verified by Google.");

        return new OAuthTokenResult
        {
            ProviderUserId = claims.Sub ?? string.Empty,
            Email = claims.Email ?? string.Empty,
            FirstName = claims.GivenName ?? string.Empty,
            LastName = claims.FamilyName ?? string.Empty,
            PhotoUrl = claims.Picture,
            AccessToken = tokenData.AccessToken,
            RefreshToken = tokenData.RefreshToken,
            ExpiresAt = tokenData.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn)
                : DateTime.UtcNow.AddHours(1)
        };
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("OAuth is only supported on iOS and macCatalyst.");
#endif
    }

    /// <summary>
    /// Verifies the id_token by calling Google's tokeninfo endpoint, which validates
    /// the RS256 signature, expiry, and audience server-side (OWASP A02).
    /// </summary>
#if IOS || MACCATALYST
    private async Task<GoogleIdTokenClaims> VerifyIdTokenAsync(string? idToken, HttpClient http)
    {
        if (string.IsNullOrEmpty(idToken))
            throw new InvalidOperationException("Google did not return an id_token.");

        var verifyUrl = $"{GoogleTokenInfoEndpoint}?id_token={Uri.EscapeDataString(idToken)}";
        _logger.LogDebug("[OAuth] Verifying id_token with Google tokeninfo endpoint...");

        var verifyResponse = await http.GetAsync(verifyUrl);
        if (!verifyResponse.IsSuccessStatusCode)
        {
            _logger.LogError("[OAuth] id_token verification failed. Status={Status}", verifyResponse.StatusCode);
            throw new UnauthorizedAccessException("Google id_token verification failed. The token may be forged, expired, or for a different app.");
        }

        var body = await verifyResponse.Content.ReadAsStringAsync();
        var claims = JsonSerializer.Deserialize<GoogleIdTokenClaims>(body);

        if (claims is null || string.IsNullOrEmpty(claims.Sub))
            throw new InvalidOperationException("Failed to parse Google tokeninfo response.");

        if (claims.Aud != _clientId)
        {
            _logger.LogError("[OAuth] id_token audience mismatch.");
            throw new UnauthorizedAccessException("Google id_token audience does not match the configured client ID.");
        }

        return claims;
    }
#endif
}

