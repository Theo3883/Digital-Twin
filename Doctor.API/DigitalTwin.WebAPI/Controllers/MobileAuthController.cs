using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DigitalTwin.Application.DTOs.MobileSync;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Handles patient authentication for mobile app.
/// </summary>
[ApiController]
[Route("api/mobile/auth")]
public class MobileAuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IUserRepository _users;
    private readonly IPatientRepository _patients;
    private readonly ILogger<MobileAuthController> _logger;

    public MobileAuthController(
        IConfiguration config,
        IUserRepository users,
        IPatientRepository patients,
        ILogger<MobileAuthController> logger)
    {
        _config = config;
        _users = users;
        _patients = patients;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a patient via Google OAuth and returns a JWT token.
    /// Creates patient account if it doesn't exist.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<PatientAuthResponse>> GoogleLogin([FromBody] PatientAuthRequest request)
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
            _logger.LogWarning(ex, "[MobileAuth] Invalid Google token: {Msg}", ex.Message);
            return Unauthorized(new { error = "Invalid Google token." });
        }

        // Get or create user
        var user = await _users.GetByEmailAsync(payload.Email);
        if (user is null)
        {
            // Create new patient user
            var nameParts = (payload.Name ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            user = new User
            {
                Email = payload.Email,
                Role = UserRole.Patient,
                FirstName = nameParts.Length > 0 ? nameParts[0] : "",
                LastName = nameParts.Length > 1 ? nameParts[1] : "",
                PhotoUrl = payload.Picture,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _users.AddAsync(user);
            _logger.LogInformation("[MobileAuth] Created new patient user: {Email} (Id={Id})", user.Email, user.Id);
        }
        else if (user.Role != UserRole.Patient)
        {
            return Forbid("This endpoint is for patients only.");
        }

        // Check if patient profile exists
        var patient = await _patients.GetByUserIdAsync(user.Id);
        var requiresProfileSetup = patient is null;

        var jwt = GenerateJwt(user);
        return Ok(new PatientAuthResponse
        {
            Token = jwt,
            ExpiresAt = DateTime.UtcNow.AddHours(24), // Longer expiry for mobile
            Email = user.Email,
            Name = user.FullName,
            UserId = user.Id,
            PatientId = patient?.Id,
            RequiresProfileSetup = requiresProfileSetup
        });
    }

    /// <summary>
    /// Returns current user and patient information for authenticated mobile user.
    /// </summary>
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Patient")]
    public async Task<ActionResult<GetMeResponse>> GetMe()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? throw new UnauthorizedAccessException("Email claim missing.");

        var user = await _users.GetByEmailAsync(email);
        if (user is null)
            return NotFound();

        var patient = await _patients.GetByUserIdAsync(user.Id);

        return Ok(new GetMeResponse
        {
            UserId = user.Id,
            PatientId = patient?.Id,
            Email = user.Email,
            FullName = user.FullName,
            PhotoUrl = user.PhotoUrl,
            RequestId = string.Empty // Not applicable for GET requests
        });
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, "Patient"),
            new(JwtRegisteredClaimNames.Sub, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var fullName = user.FullName;
        if (!string.IsNullOrEmpty(fullName))
            claims.Add(new Claim(ClaimTypes.Name, fullName));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}