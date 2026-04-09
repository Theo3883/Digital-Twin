using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Validates Google ID tokens client-side via Google's tokeninfo endpoint.
/// Same approach as the MAUI doctor portal — no backend required for auth.
/// </summary>
public class GoogleTokenValidationService
{
    private const string GoogleTokenInfoEndpoint = "https://oauth2.googleapis.com/tokeninfo";

    private readonly HttpClient _httpClient;
    private readonly string _expectedClientId;
    private readonly ILogger<GoogleTokenValidationService> _logger;

    public GoogleTokenValidationService(HttpClient httpClient, string expectedClientId, ILogger<GoogleTokenValidationService> logger)
    {
        _httpClient = httpClient;
        _expectedClientId = expectedClientId;
        _logger = logger;
    }

    /// <summary>
    /// Validates the Google ID token and returns the claims if valid.
    /// </summary>
    public async Task<GoogleIdTokenClaims?> ValidateAsync(string idToken)
    {
        try
        {
            var url = $"{GoogleTokenInfoEndpoint}?id_token={Uri.EscapeDataString(idToken)}";
            _logger.LogDebug("[GoogleTokenValidation] Verifying id_token with Google tokeninfo endpoint");

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[GoogleTokenValidation] Token verification failed: {StatusCode}", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var claims = JsonSerializer.Deserialize(body, GoogleTokenJsonContext.Default.GoogleIdTokenClaims);

            if (claims is null || string.IsNullOrEmpty(claims.Sub))
            {
                _logger.LogError("[GoogleTokenValidation] Failed to parse tokeninfo response");
                return null;
            }

            // Audience check — the token must be issued for our client ID
            if (!string.Equals(claims.Aud, _expectedClientId, StringComparison.Ordinal))
            {
                _logger.LogError("[GoogleTokenValidation] Audience mismatch: expected {Expected}, got {Actual}", _expectedClientId, claims.Aud);
                return null;
            }

            _logger.LogInformation("[GoogleTokenValidation] Token verified for {Email}", claims.Email);
            return claims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GoogleTokenValidation] Validation exception");
            return null;
        }
    }
}

/// <summary>
/// Claims extracted from Google's tokeninfo endpoint response.
/// </summary>
public record GoogleIdTokenClaims
{
    [JsonPropertyName("sub")]
    public string? Sub { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("email_verified")]
    public string? EmailVerified { get; init; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("picture")]
    public string? Picture { get; init; }

    [JsonPropertyName("aud")]
    public string? Aud { get; init; }

    [JsonPropertyName("iss")]
    public string? Iss { get; init; }

    [JsonPropertyName("exp")]
    public string? Exp { get; init; }
}

[JsonSerializable(typeof(GoogleIdTokenClaims))]
internal partial class GoogleTokenJsonContext : JsonSerializerContext;
