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
    private readonly IVitalSignRepository _vitalRepository;
    private readonly IMedicationRepository _medicationRepository;
    private readonly ISleepSessionRepository _sleepRepository;
    private readonly IEnvironmentReadingRepository _environmentRepository;
    private readonly IOcrDocumentRepository _ocrRepository;
    private readonly IMedicalHistoryEntryRepository _historyRepository;
    private readonly GoogleTokenValidationService _tokenValidator;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILocalDataResetService _localReset;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPatientRepository patientRepository,
        IVitalSignRepository vitalRepository,
        IMedicationRepository medicationRepository,
        ISleepSessionRepository sleepRepository,
        IEnvironmentReadingRepository environmentRepository,
        IOcrDocumentRepository ocrRepository,
        IMedicalHistoryEntryRepository historyRepository,
        GoogleTokenValidationService tokenValidator,
        ICloudSyncService cloudSyncService,
        ILocalDataResetService localReset,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _patientRepository = patientRepository;
        _vitalRepository = vitalRepository;
        _medicationRepository = medicationRepository;
        _sleepRepository = sleepRepository;
        _environmentRepository = environmentRepository;
        _ocrRepository = ocrRepository;
        _historyRepository = historyRepository;
        _tokenValidator = tokenValidator;
        _cloudSyncService = cloudSyncService;
        _localReset = localReset;
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

            // 2. Cloud auth first (when possible) so we can align local IDs with cloud IDs
            CloudAuthResult? cloud = null;
            try
            {
                cloud = await _cloudSyncService.AuthenticateAsync(googleIdToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[AuthService] Cloud auth failed/skipped");
            }

            // 3. Find or create local user (prefer cloud identity if available)
            var localUser = cloud?.Bootstrap?.User
                            ?? await _userRepository.GetByEmailAsync(claims.Email)
                            ?? new User { Id = Guid.NewGuid(), Email = claims.Email, Role = UserRole.Patient };

            localUser.Email = claims.Email;
            localUser.Role = UserRole.Patient;
            localUser.FirstName = claims.GivenName ?? localUser.FirstName;
            localUser.LastName = claims.FamilyName ?? localUser.LastName;
            localUser.PhotoUrl = claims.Picture ?? localUser.PhotoUrl;
            localUser.UpdatedAt = DateTime.UtcNow;
            localUser.IsSynced = cloud?.Success == true;

            await _userRepository.SaveAsync(localUser);

            // 4. Apply bootstrap (if present) so SwiftUI can skip profile completion
            if (cloud?.Success == true && cloud.Bootstrap is { } bootstrap)
            {
                // Ensure local state doesn't contain conflicting IDs from previous runs.
                await _localReset.ResetAllAsync();

                // Re-save the authenticated user after reset so the Swift layer can
                // observe an authenticated session (currentUser != nil).
                localUser.IsSynced = true;
                localUser.CreatedAt = DateTime.UtcNow;
                localUser.UpdatedAt = DateTime.UtcNow;
                await _userRepository.SaveAsync(localUser);

                await ApplyBootstrapAsync(localUser.Id, bootstrap);
                _logger.LogInformation("[AuthService] Applied cloud bootstrap for {Email}", localUser.Email);
            }
            else
            {
                // Ensure at least a local patient shell exists
                await EnsurePatientProfileAsync(localUser.Id);
            }

            _logger.LogInformation("[AuthService] Authentication successful for user {Email}", localUser.Email);

            return new AuthenticationResult
            {
                Success = true,
                AccessToken = cloud?.AccessToken,
                User = new UserDto
                {
                    Id = localUser.Id,
                    Email = localUser.Email,
                    FirstName = localUser.FirstName,
                    LastName = localUser.LastName,
                    PhotoUrl = localUser.PhotoUrl
                },
                HasCloudProfile = cloud?.Bootstrap?.Patient != null
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

    private async Task ApplyBootstrapAsync(Guid localUserId, CloudBootstrap bootstrap)
    {
        // Ensure a local user row exists that matches cloud identity.
        if (bootstrap.User is { } cloudUser)
        {
            cloudUser.Id = localUserId;
            cloudUser.IsSynced = true;
            cloudUser.UpdatedAt = DateTime.UtcNow;
            await _userRepository.SaveAsync(cloudUser);
        }

        // Now save patient + related tables.
        if (bootstrap.Patient is { } patient)
        {
            // Ensure patient points at the local user ID (cloud UserId should match anyway).
            patient.UserId = localUserId;
            patient.IsSynced = true;
            patient.UpdatedAt = DateTime.UtcNow;
            await _patientRepository.SaveAsync(patient);

            var pid = patient.Id;

            // Vitals
            foreach (var v in bootstrap.Vitals)
            {
                v.PatientId = pid;
                v.IsSynced = true;
            }
            await _vitalRepository.SaveRangeAsync(bootstrap.Vitals);

            // Medications
            foreach (var m in bootstrap.Medications)
            {
                m.PatientId = pid;
                m.IsSynced = true;
                await _medicationRepository.SaveAsync(m);
            }

            // Sleep
            foreach (var s in bootstrap.SleepSessions)
            {
                s.PatientId = pid;
                s.IsSynced = true;
            }
            await _sleepRepository.SaveRangeAsync(bootstrap.SleepSessions);

            // Environment (not patient-scoped in local schema)
            foreach (var e in bootstrap.EnvironmentReadings)
            {
                e.IsDirty = false;
                e.SyncedAt = DateTime.UtcNow;
                await _environmentRepository.SaveAsync(e);
            }

            // OCR documents
            foreach (var d in bootstrap.OcrDocuments)
            {
                d.PatientId = pid;
                d.IsDirty = false;
                d.SyncedAt = DateTime.UtcNow;
                await _ocrRepository.SaveAsync(d);
            }

            // Medical history entries
            foreach (var h in bootstrap.MedicalHistoryEntries)
            {
                h.PatientId = pid;
                h.IsDirty = false;
                h.SyncedAt = DateTime.UtcNow;
                h.UpdatedAt = DateTime.UtcNow;
            }
            await _historyRepository.SaveRangeAsync(bootstrap.MedicalHistoryEntries);
        }
    }
}