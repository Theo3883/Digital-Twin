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

    // ── Medications Sync ──────────────────────────────────────────────────────

    public async Task<bool> SyncMedicationsAsync(IEnumerable<Medication> medications)
    {
        try
        {
            EnsureAuthenticated();
            var items = medications.Select(m => new MedicationSyncItem
            {
                Id = m.Id, Name = m.Name, Dosage = m.Dosage,
                Frequency = m.Frequency, Route = (int)m.Route,
                RxCui = m.RxCui, Instructions = m.Instructions,
                Reason = m.Reason, StartDate = m.StartDate, EndDate = m.EndDate,
                Status = (int)m.Status, DiscontinuedReason = m.DiscontinuedReason,
                AddedByRole = (int)m.AddedByRole,
                CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
            }).ToList();

            var request = new DeviceRequestEnvelope<MedicationSyncItem>
            {
                DeviceId = GetDeviceId(), RequestId = Guid.NewGuid().ToString(), Items = items
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeMedicationSyncItem);
            var response = await _httpClient.PostAsync("/api/mobile/sync/medications/upsert", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync medications");
            return false;
        }
    }

    public async Task<IEnumerable<Medication>> GetMedicationsAsync()
    {
        try
        {
            EnsureAuthenticated();
            var response = await _httpClient.GetAsync("/api/mobile/sync/medications");
            if (!response.IsSuccessStatusCode) return [];
            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.MedicationSyncResponse);
            if (result?.Items == null) return [];
            return result.Items.Select(m => new Medication
            {
                Id = m.Id, Name = m.Name, Dosage = m.Dosage,
                Frequency = m.Frequency, Route = (MedicationRoute)m.Route,
                RxCui = m.RxCui, Instructions = m.Instructions, Reason = m.Reason,
                StartDate = m.StartDate, EndDate = m.EndDate,
                Status = (MedicationStatus)m.Status,
                DiscontinuedReason = m.DiscontinuedReason,
                AddedByRole = (AddedByRole)m.AddedByRole,
                CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to get medications");
            return [];
        }
    }

    // ── Sleep Sync ────────────────────────────────────────────────────────────

    public async Task<bool> SyncSleepSessionsAsync(IEnumerable<SleepSession> sessions)
    {
        try
        {
            EnsureAuthenticated();
            var items = sessions.Select(s => new SleepSyncItem
            {
                StartTime = s.StartTime, EndTime = s.EndTime,
                DurationMinutes = s.DurationMinutes, QualityScore = s.QualityScore
            }).ToList();

            var request = new DeviceRequestEnvelope<SleepSyncItem>
            {
                DeviceId = GetDeviceId(), RequestId = Guid.NewGuid().ToString(), Items = items
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeSleepSyncItem);
            var response = await _httpClient.PostAsync("/api/mobile/sync/sleep/append", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync sleep sessions");
            return false;
        }
    }

    // ── Environment Sync ──────────────────────────────────────────────────────

    public async Task<bool> SyncEnvironmentReadingsAsync(IEnumerable<EnvironmentReading> readings)
    {
        try
        {
            EnsureAuthenticated();
            var items = readings.Select(e => new EnvironmentSyncItem
            {
                Latitude = e.Latitude, Longitude = e.Longitude,
                LocationDisplayName = e.LocationDisplayName,
                PM25 = e.PM25, PM10 = e.PM10, O3 = e.O3, NO2 = e.NO2,
                Temperature = e.Temperature, Humidity = e.Humidity,
                AirQuality = (int)e.AirQuality, AqiIndex = e.AqiIndex,
                Timestamp = e.Timestamp
            }).ToList();

            var request = new DeviceRequestEnvelope<EnvironmentSyncItem>
            {
                DeviceId = GetDeviceId(), RequestId = Guid.NewGuid().ToString(), Items = items
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeEnvironmentSyncItem);
            var response = await _httpClient.PostAsync("/api/mobile/sync/environment/append", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync environment readings");
            return false;
        }
    }

    // ── OCR Documents Sync ────────────────────────────────────────────────────

    public async Task<bool> SyncOcrDocumentsAsync(IEnumerable<OcrDocument> documents)
    {
        try
        {
            EnsureAuthenticated();
            var items = documents.Select(d => new OcrDocumentSyncItem
            {
                Id = d.Id, OpaqueInternalName = d.OpaqueInternalName,
                MimeType = d.MimeType, PageCount = d.PageCount,
                SanitizedOcrPreview = d.SanitizedOcrPreview,
                ScannedAt = d.ScannedAt
            }).ToList();

            var request = new DeviceRequestEnvelope<OcrDocumentSyncItem>
            {
                DeviceId = GetDeviceId(), RequestId = Guid.NewGuid().ToString(), Items = items
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeOcrDocumentSyncItem);
            var response = await _httpClient.PostAsync("/api/mobile/sync/ocr-documents/upsert", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync OCR documents");
            return false;
        }
    }

    // ── Medical History Sync ──────────────────────────────────────────────────

    public async Task<bool> SyncMedicalHistoryAsync(IEnumerable<MedicalHistoryEntry> entries)
    {
        try
        {
            EnsureAuthenticated();
            var items = entries.Select(e => new MedicalHistorySyncItem
            {
                Id = e.Id, SourceDocumentId = e.SourceDocumentId,
                Title = e.Title, MedicationName = e.MedicationName,
                Dosage = e.Dosage, Frequency = e.Frequency,
                Duration = e.Duration, Notes = e.Notes,
                Summary = e.Summary, Confidence = e.Confidence,
                EventDate = e.EventDate
            }).ToList();

            var request = new DeviceRequestEnvelope<MedicalHistorySyncItem>
            {
                DeviceId = GetDeviceId(), RequestId = Guid.NewGuid().ToString(), Items = items
            };

            using var content = JsonContent.Create(request, InfrastructureJsonContext.Default.DeviceRequestEnvelopeMedicalHistorySyncItem);
            var response = await _httpClient.PostAsync("/api/mobile/sync/medical-history/append", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Failed to sync medical history");
            return false;
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