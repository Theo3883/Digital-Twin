using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Auth;

/// <summary>
/// Claims returned by Google's tokeninfo endpoint after RS256 signature validation.
/// Only fields used by the app are mapped; Google returns additional claims.
/// </summary>
internal record GoogleIdTokenClaims
{
    /// <summary>Stable, unique user identifier from Google.</summary>
    [JsonPropertyName("sub")]
    public string? Sub { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("picture")]
    public string? Picture { get; init; }

    /// <summary>Audience — must match the app's configured client ID (OWASP A02 check).</summary>
    [JsonPropertyName("aud")]
    public string? Aud { get; init; }

    /// <summary>Issuer — should be accounts.google.com or https://accounts.google.com.</summary>
    [JsonPropertyName("iss")]
    public string? Iss { get; init; }
}
