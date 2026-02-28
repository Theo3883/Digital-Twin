using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Handles doctor authentication and registration.
/// Google id_token is validated server-side; if the doctor doesn't exist yet
/// the frontend must supply a registration secret to create the account.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IUserRepository _users;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration config,
        IUserRepository users,
        ILogger<AuthController> logger)
    {
        _config = config;
        _users = users;
        _logger = logger;
    }

    // ── Request / Response records ───────────────────────────────────────────

    public record GoogleLoginRequest(string IdToken);
    public record RegisterRequest(string IdToken, string DoctorSecret);

    public record LoginResponse(
        string Token,
        DateTime ExpiresAt,
        string Email,
        string? Name,
        bool RegistrationRequired = false);

    // ── POST /api/auth/google ────────────────────────────────────────────────

    /// <summary>
    /// Validates the Google id_token and returns a JWT if the doctor already
    /// exists in the database.  If the doctor is unknown, returns
    /// <c>registrationRequired: true</c> so the frontend can ask for the
    /// registration secret.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<LoginResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_config["Google:ClientId"]]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("[Auth] Invalid Google token: {Msg}", ex.Message);
            return Unauthorized(new { error = "Invalid Google token." });
        }

        // Check if a User with this email already exists.
        var user = await _users.GetByEmailAsync(payload.Email);

        if (user is null)
        {
            // Doctor not registered — tell the frontend to collect the secret.
            return Ok(new LoginResponse(
                Token: "",
                ExpiresAt: DateTime.MinValue,
                Email: payload.Email,
                Name: payload.Name,
                RegistrationRequired: true));
        }

        if (user.Role != UserRole.Doctor)
        {
            return Forbid();
        }

        var jwt = GenerateJwt(user);
        return Ok(new LoginResponse(jwt, DateTime.UtcNow.AddHours(8), user.Email,
            $"{user.FirstName} {user.LastName}".Trim()));
    }

    // ── POST /api/auth/register ──────────────────────────────────────────────

    /// <summary>
    /// Creates a new Doctor account after verifying the registration secret.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        // Validate the doctor registration secret.
        var expectedSecret = _config["Doctor:RegistrationSecret"];
        if (string.IsNullOrEmpty(expectedSecret))
        {
            _logger.LogError("[Auth] Doctor:RegistrationSecret is not configured.");
            return StatusCode(500, new { error = "Registration is not configured." });
        }

        if (!string.Equals(request.DoctorSecret, expectedSecret, StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "Invalid registration secret." });
        }

        // Validate the Google token.
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_config["Google:ClientId"]]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("[Auth] Invalid Google token on register: {Msg}", ex.Message);
            return Unauthorized(new { error = "Invalid Google token." });
        }

        // Prevent duplicate registration.
        var existing = await _users.GetByEmailAsync(payload.Email);
        if (existing is not null)
        {
            if (existing.Role != UserRole.Doctor)
                return Forbid();

            var jwt2 = GenerateJwt(existing);
            return Ok(new LoginResponse(jwt2, DateTime.UtcNow.AddHours(8), existing.Email,
                $"{existing.FirstName} {existing.LastName}".Trim()));
        }

        // Create the new doctor User.
        var nameParts = (payload.Name ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var doctor = new User
        {
            Email = payload.Email,
            Role = UserRole.Doctor,
            FirstName = nameParts.Length > 0 ? nameParts[0] : "",
            LastName = nameParts.Length > 1 ? nameParts[1] : "",
            PhotoUrl = payload.Picture,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _users.AddAsync(doctor);
        _logger.LogInformation("[Auth] Registered new doctor: {Email} (Id={Id})", doctor.Email, doctor.Id);

        var jwt = GenerateJwt(doctor);
        return Ok(new LoginResponse(jwt, DateTime.UtcNow.AddHours(8), doctor.Email,
            $"{doctor.FirstName} {doctor.LastName}".Trim()));
    }

    // ── JWT generation ───────────────────────────────────────────────────────

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, "Doctor"),
            new(JwtRegisteredClaimNames.Sub, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrEmpty(fullName))
            claims.Add(new Claim(ClaimTypes.Name, fullName));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
