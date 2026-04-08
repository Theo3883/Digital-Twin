using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Application service for authentication orchestration
/// </summary>
public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPatientRepository patientRepository,
        ICloudSyncService cloudSyncService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _patientRepository = patientRepository;
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates user with Google ID token and sets up local profile
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateWithGoogleAsync(string googleIdToken)
    {
        try
        {
            _logger.LogInformation("[AuthService] Starting Google authentication");

            // Authenticate with cloud service
            var success = await _cloudSyncService.AuthenticateAsync(googleIdToken);
            if (!success)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Authentication failed"
                };
            }

            // Get user profile from cloud
            var cloudUser = await _cloudSyncService.GetCurrentUserProfileAsync();
            if (cloudUser == null)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve user profile"
                };
            }

            // Save/update local user
            var localUser = await _userRepository.GetByEmailAsync(cloudUser.Email);
            if (localUser == null)
            {
                localUser = new User
                {
                    Id = cloudUser.Id,
                    Email = cloudUser.Email,
                    Role = UserRole.Patient, // Mobile app is for patients
                    FirstName = cloudUser.FirstName,
                    LastName = cloudUser.LastName,
                    PhotoUrl = cloudUser.PhotoUrl,
                    Phone = cloudUser.Phone,
                    DateOfBirth = cloudUser.DateOfBirth,
                    IsSynced = true
                };
            }
            else
            {
                // Update existing user
                localUser.FirstName = cloudUser.FirstName ?? localUser.FirstName;
                localUser.LastName = cloudUser.LastName ?? localUser.LastName;
                localUser.PhotoUrl = cloudUser.PhotoUrl ?? localUser.PhotoUrl;
                localUser.Phone = cloudUser.Phone ?? localUser.Phone;
                localUser.DateOfBirth = cloudUser.DateOfBirth ?? localUser.DateOfBirth;
                localUser.UpdatedAt = DateTime.UtcNow;
                localUser.IsSynced = true;
            }

            await _userRepository.SaveAsync(localUser);

            // Ensure patient profile exists
            await EnsurePatientProfileAsync(localUser.Id);

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