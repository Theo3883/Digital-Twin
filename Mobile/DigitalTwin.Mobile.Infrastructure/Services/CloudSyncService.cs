using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

/// <summary>
/// HTTP client implementation that calls the existing DigitalTwin WebAPI
/// </summary>
public class CloudSyncService : ICloudSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudSyncService> _logger;
    private string? _accessToken;

    public CloudSyncService(HttpClient httpClient, ILogger<CloudSyncService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ── Authentication ────────────────────────────────────────────────────────

    public async Task<bool> AuthenticateAsync(string googleIdToken)
    {
        try
        {
            var request = new GoogleAuthRequest
            {
                GoogleIdToken = googleIdToken,
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.GoogleAuthRequest);
            var response = await _httpClient.PostAsync("/api/mobile/auth/google", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[CloudSync] Authentication failed: {StatusCode}", response.StatusCode);
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.AuthResponse);
            if (result?.Success == true && !string.IsNullOrEmpty(result.AccessToken))
            {
                _accessToken = result.AccessToken;
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", _accessToken);
                
                _logger.LogInformation("[CloudSync] Authentication successful");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Authentication exception");
            return false;
        }
    }

    public async Task<User?> GetCurrentUserProfileAsync()
    {
        try
        {
            EnsureAuthenticated();
            
            var response = await _httpClient.GetAsync("/api/mobile/auth/me");
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.UserProfileResponse);
            if (result?.User == null) return null;

            return new User
            {
                Id = result.User.Id,
                Email = result.User.Email,
                Role = UserRole.Patient,
                FirstName = result.User.FirstName,
                LastName = result.User.LastName,
                PhotoUrl = result.User.PhotoUrl,
                Phone = result.User.Phone,
                DateOfBirth = result.User.DateOfBirth
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to get user profile");
            return null;
        }
    }

    // ── User Sync ─────────────────────────────────────────────────────────────

    public async Task<bool> SyncUserAsync(User user)
    {
        try
        {
            EnsureAuthenticated();

            var request = new DeviceRequestEnvelope<UpsertUserRequest>
            {
                DeviceId = GetDeviceId(),
                RequestId = Guid.NewGuid().ToString(),
                User = new UpsertUserRequest
                {
                    Email = user.Email,
                    Role = (int)user.Role,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhotoUrl = user.PhotoUrl,
                    Phone = user.Phone,
                    Address = null,
                    City = null,
                    Country = null,
                    DateOfBirth = user.DateOfBirth
                }
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeUpsertUserRequest);
            var response = await _httpClient.PostAsync("/api/mobile/sync/users/upsert", content);
            
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.SyncResponse);
                return result?.Success == true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync user");
            return false;
        }
    }

    // ── Patient Sync ──────────────────────────────────────────────────────────

    public async Task<bool> SyncPatientAsync(Patient patient)
    {
        try
        {
            EnsureAuthenticated();

            var request = new DeviceRequestEnvelope<UpsertPatientRequest>
            {
                DeviceId = GetDeviceId(),
                RequestId = Guid.NewGuid().ToString(),
                Patient = new UpsertPatientRequest
                {
                    BloodType = patient.BloodType,
                    Allergies = patient.Allergies,
                    MedicalHistoryNotes = patient.MedicalHistoryNotes,
                    Weight = patient.Weight,
                    Height = patient.Height,
                    BloodPressureSystolic = patient.BloodPressureSystolic,
                    BloodPressureDiastolic = patient.BloodPressureDiastolic,
                    Cholesterol = patient.Cholesterol,
                    Cnp = patient.Cnp
                }
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeUpsertPatientRequest);
            var response = await _httpClient.PostAsync("/api/mobile/sync/patients/upsert", content);
            
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.SyncResponse);
                return result?.Success == true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync patient");
            return false;
        }
    }

    public async Task<Patient?> GetPatientProfileAsync()
    {
        try
        {
            EnsureAuthenticated();
            
            var response = await _httpClient.GetAsync("/api/mobile/sync/patients/profile");
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.PatientProfileResponse);
            if (result?.Patient == null) return null;

            return new Patient
            {
                BloodType = result.Patient.BloodType,
                Allergies = result.Patient.Allergies,
                MedicalHistoryNotes = result.Patient.MedicalHistoryNotes,
                Weight = result.Patient.Weight,
                Height = result.Patient.Height,
                BloodPressureSystolic = result.Patient.BloodPressureSystolic,
                BloodPressureDiastolic = result.Patient.BloodPressureDiastolic,
                Cholesterol = result.Patient.Cholesterol,
                Cnp = result.Patient.Cnp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to get patient profile");
            return null;
        }
    }

    // ── Vital Signs Sync ──────────────────────────────────────────────────────

    public async Task<bool> SyncVitalSignsAsync(IEnumerable<VitalSign> vitalSigns)
    {
        try
        {
            EnsureAuthenticated();

            var items = vitalSigns.Select(v => new VitalAppendRequestItem
            {
                Type = (int)v.Type,
                Value = v.Value,
                Unit = v.Unit,
                Source = v.Source,
                Timestamp = v.Timestamp
            }).ToList();

            var request = new DeviceRequestEnvelope<VitalAppendRequestItem>
            {
                DeviceId = GetDeviceId(),
                RequestId = Guid.NewGuid().ToString(),
                Items = items
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeVitalAppendRequestItem);
            var response = await _httpClient.PostAsync("/api/mobile/sync/vitals/append", content);
            
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.VitalSyncResponse);
                _logger.LogInformation("[CloudSync] Synced {AcceptedCount} vitals, {DedupedCount} duplicates", 
                    result?.AcceptedCount ?? 0, result?.DedupedCount ?? 0);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync vital signs");
            return false;
        }
    }

    public async Task<IEnumerable<VitalSign>> GetVitalSignsAsync(DateTime fromDate, DateTime? toDate = null)
    {
        try
        {
            EnsureAuthenticated();
            
            var queryParams = new List<string>
            {
                $"fromUtc={fromDate:O}"
            };
            
            if (toDate.HasValue)
                queryParams.Add($"toUtc={toDate.Value:O}");

            var queryString = "?" + string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"/api/mobile/sync/vitals{queryString}");
            
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.VitalSignsResponse);
            if (result?.Items == null) return [];

            return result.Items.Select(item => new VitalSign
            {
                Type = (VitalSignType)item.Type,
                Value = item.Value,
                Unit = item.Unit,
                Source = item.Source,
                Timestamp = item.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to get vital signs");
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EnsureAuthenticated()
    {
        if (string.IsNullOrEmpty(_accessToken))
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");
    }

    private static string GetDeviceId()
    {
        return Environment.MachineName ?? "mobile-device";
    }

    // Note: DTOs moved to `CloudSyncDtos.cs` for NativeAOT-safe source generation.
}