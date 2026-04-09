using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Application service for authentication orchestration.
/// Validates Google ID tokens client-side (like the MAUI portal) — no backend required.
/// Cloud sync happens separately when API_BASE_URL is configured.
/// </summary>
public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly GoogleTokenValidationService _tokenValidator;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPatientRepository patientRepository,
        GoogleTokenValidationService tokenValidator,
        ICloudSyncService cloudSyncService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _patientRepository = patientRepository;
        _tokenValidator = tokenValidator;
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates user with Google ID token — validates client-side, creates local user.
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateWithGoogleAsync(string googleIdToken)
    {
        try
        {
            _logger.LogInformation("[AuthService] Starting Google authentication (client-side validation)");

            // 1. Validate token directly with Google (no backend needed)
            var claims = await _tokenValidator.ValidateAsync(googleIdToken);
            if (claims == null || string.IsNullOrEmpty(claims.Email))
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Google token validation failed"
                };
            }

            // 2. Find or create local user from token claims
            var localUser = await _userRepository.GetByEmailAsync(claims.Email);
            if (localUser == null)
            {
                localUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = claims.Email,
                    Role = UserRole.Patient,
                    FirstName = claims.GivenName,
                    LastName = claims.FamilyName,
                    PhotoUrl = claims.Picture,
                    IsSynced = false
                };
            }
            else
            {
                // Update from latest token claims
                localUser.FirstName = claims.GivenName ?? localUser.FirstName;
                localUser.LastName = claims.FamilyName ?? localUser.LastName;
                localUser.PhotoUrl = claims.Picture ?? localUser.PhotoUrl;
                localUser.UpdatedAt = DateTime.UtcNow;
            }

            await _userRepository.SaveAsync(localUser);

            // 3. Ensure patient profile exists
            await EnsurePatientProfileAsync(localUser.Id);

            // 4. Best-effort cloud sync (non-blocking, only if API is configured)
            _ = Task.Run(async () =>
            {
                try
                {
                    var synced = await _cloudSyncService.AuthenticateAsync(googleIdToken);
                    if (synced)
                    {
                        localUser.IsSynced = true;
                        await _userRepository.SaveAsync(localUser);
                        _logger.LogInformation("[AuthService] Cloud sync succeeded for {Email}", localUser.Email);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[AuthService] Cloud sync skipped (no backend or offline)");
                }
            });

            _logger.LogInformation("[AuthService] Authentication successful for user {Email}", localUser.Email);

            return new AuthenticationResult
            {
                Success = true,
                User = new UserDto
                {
                    Id = localUser.Id,
                    Email = localUser.Email,
                    FirstName = localUser.FirstName,
                    LastName = localUser.LastName,
                    PhotoUrl = localUser.PhotoUrl
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService] Authentication failed");
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = "Authentication failed: " + ex.Message
            };
        }
    }

    /// <summary>
    /// Gets current authenticated user
    /// </summary>
    public async Task<UserDto?> GetCurrentUserAsync()
    {
        var user = await _userRepository.GetCurrentUserAsync();
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhotoUrl = user.PhotoUrl
        };
    }

    private async Task EnsurePatientProfileAsync(Guid userId)
    {
        var patient = await _patientRepository.GetByUserIdAsync(userId);
        if (patient == null)
        {
            // Create new patient profile
            patient = new Patient
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IsSynced = false // Will be synced later
            };
            await _patientRepository.SaveAsync(patient);
        }
    }
}