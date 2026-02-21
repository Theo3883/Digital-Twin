using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Services;

public class AuthApplicationService : IAuthApplicationService
{
    private readonly IOAuthTokenProvider _tokenProvider;
    private readonly ISecureTokenStorage _tokenStorage;
    private readonly IUserRepository _userRepo;
    private readonly IUserOAuthRepository _oauthRepo;
    private readonly IPatientRepository _patientRepo;

    private AuthResultDto? _cachedUser;

    private const string UserIdKey = "auth_user_id";
    private const string PatientIdKey = "auth_patient_id";

    public AuthApplicationService(
        IOAuthTokenProvider tokenProvider,
        ISecureTokenStorage tokenStorage,
        IUserRepository userRepo,
        IUserOAuthRepository oauthRepo,
        IPatientRepository patientRepo)
    {
        _tokenProvider = tokenProvider;
        _tokenStorage = tokenStorage;
        _userRepo = userRepo;
        _oauthRepo = oauthRepo;
        _patientRepo = patientRepo;
    }

    public async Task<AuthResultDto> SignInWithGoogleAsync()
    {
        var tokens = await _tokenProvider.GetTokensAsync();

        var existingOAuth = await _oauthRepo.FindByProviderAndUserIdAsync(
            OAuthProvider.Google, tokens.ProviderUserId);

        User user;
        Patient patient;

        if (existingOAuth is not null)
        {
            user = (await _userRepo.GetByIdAsync(existingOAuth.UserId))!;
            patient = (await _patientRepo.GetByUserIdAsync(user.Id))!;

            existingOAuth.AccessToken = tokens.AccessToken;
            existingOAuth.RefreshToken = tokens.RefreshToken;
            existingOAuth.ExpiresAt = tokens.ExpiresAt;
            existingOAuth.Email = tokens.Email;
            await _oauthRepo.UpdateAsync(existingOAuth);
        }
        else
        {
            user = new User
            {
                Email = tokens.Email,
                Role = UserRole.Patient,
                FirstName = tokens.FirstName,
                LastName = tokens.LastName,
                PhotoUrl = tokens.PhotoUrl
            };
            await _userRepo.AddAsync(user);

            patient = new Patient { UserId = user.Id };
            await _patientRepo.AddAsync(patient);

            var oauth = new UserOAuth
            {
                UserId = user.Id,
                Provider = OAuthProvider.Google,
                ProviderUserId = tokens.ProviderUserId,
                Email = tokens.Email,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = tokens.ExpiresAt
            };
            await _oauthRepo.AddAsync(oauth);
        }

        await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());
        await _tokenStorage.StoreAsync(PatientIdKey, patient.Id.ToString());

        _cachedUser = new AuthResultDto
        {
            UserId = user.Id,
            PatientId = patient.Id,
            Email = user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            PhotoUrl = user.PhotoUrl
        };

        return _cachedUser;
    }

    public async Task SignOutAsync()
    {
        _cachedUser = null;
        await _tokenStorage.ClearAllAsync();
    }

    public async Task<AuthResultDto?> GetCurrentUserAsync()
    {
        if (_cachedUser is not null) return _cachedUser;

        var userIdStr = await _tokenStorage.GetAsync(UserIdKey);
        if (userIdStr is null || !long.TryParse(userIdStr, out var userId))
            return null;

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null) return null;

        var patient = await _patientRepo.GetByUserIdAsync(userId);
        if (patient is null) return null;

        _cachedUser = new AuthResultDto
        {
            UserId = user.Id,
            PatientId = patient.Id,
            Email = user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            PhotoUrl = user.PhotoUrl
        };

        return _cachedUser;
    }

    public async Task<long?> GetCurrentPatientIdAsync()
    {
        var current = await GetCurrentUserAsync();
        return current?.PatientId;
    }
}
