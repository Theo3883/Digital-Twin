using System.Security.Claims;
using System.Text.Encodings.Web;
using DigitalTwin.Domain.Enums;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DigitalTwin.WebAPI.Auth;

/// <summary>
/// Authenticates requests using a Google ID token provided as:
/// Authorization: Bearer &lt;google_id_token&gt;
/// </summary>
public sealed class GoogleBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _config;

    public GoogleBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config)
        : base(options, logger, encoder)
    {
        _config = config;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return AuthenticateResult.Fail("Missing bearer token.");

        string[] audiences = GetGoogleAudiences();
        if (audiences.Length == 0)
            return AuthenticateResult.Fail("Server misconfiguration: missing Google client id.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = audiences
            });
        }
        catch (InvalidJwtException ex)
        {
            return AuthenticateResult.Fail($"Invalid Google token: {ex.Message}");
        }

        // Create a principal. Google id_token doesn't carry app roles; default to Patient.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, payload.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, payload.Subject ?? payload.Email ?? string.Empty),
            new(ClaimTypes.Name, payload.Name ?? string.Empty),
            new(ClaimTypes.Role, UserRole.Patient.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private string[] GetGoogleAudiences()
    {
        // Allow mobile + web client IDs.
        var mobileSingle = _config["Google:MobileClientId"];
        var mobileMany = _config["Google:MobileClientIds"];
        var defaultClient = _config["Google:ClientId"];

        static IEnumerable<string> SplitCsv(string? s) =>
            string.IsNullOrWhiteSpace(s)
                ? []
                : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var values = new List<string>();
        values.AddRange(SplitCsv(mobileMany));
        if (!string.IsNullOrWhiteSpace(mobileSingle)) values.Add(mobileSingle);
        if (!string.IsNullOrWhiteSpace(defaultClient)) values.Add(defaultClient);
        return values.Distinct(StringComparer.Ordinal).ToArray();
    }
}

