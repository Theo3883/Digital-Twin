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
    private readonly IAccessTokenStore _tokenStore;
    private readonly bool _isOffline;
    private DateTime _circuitOpenUntilUtc = DateTime.MinValue;
    private static readonly TimeSpan CircuitCooldown = TimeSpan.FromSeconds(30);
    private bool IsCircuitOpen => DateTime.UtcNow < _circuitOpenUntilUtc;

    public bool IsAuthenticated => !_isOffline && !string.IsNullOrEmpty(_tokenStore.AccessToken);

    public CloudSyncService(HttpClient httpClient, IAccessTokenStore tokenStore, ILogger<CloudSyncService> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
        _isOffline = _httpClient.BaseAddress == null;
        if (_isOffline)
        {
            _logger.LogWarning("[CloudSync] Offline mode: HttpClient BaseAddress is not configured");
        }
    }

    private void TripCircuit(string reason)
    {
        _circuitOpenUntilUtc = DateTime.UtcNow + CircuitCooldown;
        _logger.LogWarning(
            "[CloudSync] Circuit breaker tripped ({Reason}) — skipping cloud calls for {Seconds}s",
            reason,
            CircuitCooldown.TotalSeconds);
    }

    public async Task<bool> IsCloudReachableAsync(CancellationToken ct = default)
    {
        if (_isOffline) return false;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await _httpClient.GetAsync("/api/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Authentication ────────────────────────────────────────────────────────

    public async Task<CloudAuthResult> AuthenticateAsync(string googleIdToken)
    {
        try
        {
            if (_isOffline) return new CloudAuthResult { Success = false, ErrorMessage = "Offline mode" };

            // Google-only mode: we will use the Google ID token as the Bearer token for all API calls.
            // We still call the bootstrap endpoint to verify the token and hydrate initial state.
            _tokenStore.AccessToken = googleIdToken;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

            var request = new GoogleAuthRequest
            {
                GoogleIdToken = googleIdToken,
            };

            // Verify the token by calling a Google-protected endpoint.
            // This avoids server-minted JWTs entirely (Google-only mode).
            var response = await _httpClient.GetAsync("/api/mobile/auth/me");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[CloudSync] Authentication failed: {StatusCode}", response.StatusCode);
                _tokenStore.Clear();
                return new CloudAuthResult { Success = false, ErrorMessage = $"HTTP {(int)response.StatusCode}" };
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var me = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.UserProfileResponse);
            _logger.LogInformation(
                "[CloudDebug][CloudSync] Google token accepted by server. meEmail={Email}",
                me?.User?.Email ?? "nil");
            if (!string.IsNullOrWhiteSpace(me?.User?.Email))
            {
                _logger.LogInformation("[CloudSync] Google token accepted by server");
                return new CloudAuthResult
                {
                    Success = true,
                    // For UI/state in Swift: treat the Google ID token as the "access token".
                    AccessToken = googleIdToken,
                    Bootstrap = null,
                };
            }

            _tokenStore.Clear();
            return new CloudAuthResult { Success = false, ErrorMessage = "Authentication failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloudSync] Authentication exception");
            _tokenStore.Clear();
            return new CloudAuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static CloudBootstrap? MapBootstrap(MobileBootstrapDto? bootstrap)
    {
        if (bootstrap?.User is null) return null;

        var user = new User
        {
            Id = bootstrap.User.Id,
            Email = bootstrap.User.Email,
            Role = (UserRole)bootstrap.User.Role,
            FirstName = bootstrap.User.FirstName,
            LastName = bootstrap.User.LastName,
            PhotoUrl = bootstrap.User.PhotoUrl,
            Phone = bootstrap.User.Phone,
            Address = bootstrap.User.Address,
            City = bootstrap.User.City,
            Country = bootstrap.User.Country,
            DateOfBirth = bootstrap.User.DateOfBirth,
            IsSynced = true
        };

        Patient? patient = null;
        if (bootstrap.Patient is not null)
        {
            patient = new Patient
            {
                Id = bootstrap.Patient.Id,
                UserId = bootstrap.Patient.UserId,
                BloodType = bootstrap.Patient.BloodType,
                Allergies = bootstrap.Patient.Allergies,
                MedicalHistoryNotes = bootstrap.Patient.MedicalHistoryNotes,
                Weight = bootstrap.Patient.Weight,
                Height = bootstrap.Patient.Height,
                BloodPressureSystolic = bootstrap.Patient.BloodPressureSystolic,
                BloodPressureDiastolic = bootstrap.Patient.BloodPressureDiastolic,
                Cholesterol = bootstrap.Patient.Cholesterol,
                Cnp = bootstrap.Patient.Cnp,
                IsSynced = true,
                UpdatedAt = DateTime.UtcNow
            };
        }

        var vitals = (bootstrap.Vitals ?? []).Select(v => new VitalSign
        {
            Id = v.Id ?? Guid.NewGuid(),
            PatientId = patient?.Id ?? Guid.Empty,
            Type = (VitalSignType)v.Type,
            Value = v.Value,
            Unit = v.Unit,
            Source = v.Source,
            Timestamp = v.Timestamp,
            IsSynced = true
        }).ToArray();

        var medications = (bootstrap.Medications ?? []).Select(m => new Medication
        {
            Id = m.Id,
            PatientId = patient?.Id ?? Guid.Empty,
            Name = m.Name,
            Dosage = m.Dosage,
            Frequency = m.Frequency,
            Route = (MedicationRoute)m.Route,
            RxCui = m.RxCui,
            Instructions = m.Instructions,
            Reason = m.Reason,
            StartDate = m.StartDate,
            EndDate = m.EndDate,
            Status = (MedicationStatus)m.Status,
            DiscontinuedReason = m.DiscontinuedReason,
            AddedByRole = (AddedByRole)m.AddedByRole,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
            IsSynced = true
        }).ToArray();

        var sleep = (bootstrap.SleepSessions ?? []).Select(s => new SleepSession
        {
            Id = Guid.NewGuid(),
            PatientId = patient?.Id ?? Guid.Empty,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationMinutes = s.DurationMinutes,
            QualityScore = s.QualityScore,
            CreatedAt = DateTime.UtcNow,
            IsSynced = true
        }).ToArray();

        var env = (bootstrap.EnvironmentReadings ?? []).Select(e => new EnvironmentReading
        {
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            LocationDisplayName = e.LocationDisplayName,
            PM25 = e.PM25,
            PM10 = e.PM10,
            O3 = e.O3,
            NO2 = e.NO2,
            Temperature = e.Temperature,
            Humidity = e.Humidity,
            AirQuality = (AirQualityLevel)e.AirQuality,
            AqiIndex = e.AqiIndex,
            Timestamp = e.Timestamp,
            IsDirty = false,
            SyncedAt = DateTime.UtcNow
        }).ToArray();

        var ocr = (bootstrap.OcrDocuments ?? []).Select(d => new OcrDocument
        {
            Id = d.Id,
            PatientId = patient?.Id ?? Guid.Empty,
            OpaqueInternalName = d.OpaqueInternalName,
            MimeType = d.MimeType,
            DocumentType = d.DocumentType,
            PageCount = d.PageCount,
            Sha256OfNormalized = d.Sha256OfNormalized ?? string.Empty,
            SanitizedOcrPreview = d.SanitizedOcrPreview,
            ScannedAt = d.ScannedAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDirty = false,
            SyncedAt = DateTime.UtcNow
        }).ToArray();

        var history = (bootstrap.MedicalHistoryEntries ?? []).Select(h => new MedicalHistoryEntry
        {
            Id = h.Id,
            PatientId = patient?.Id ?? Guid.Empty,
            SourceDocumentId = h.SourceDocumentId,
            Title = h.Title,
            MedicationName = h.MedicationName,
            Dosage = h.Dosage,
            Frequency = h.Frequency,
            Duration = h.Duration,
            Notes = h.Notes,
            Summary = h.Summary,
            Confidence = h.Confidence,
            EventDate = h.EventDate,
            IsDirty = false,
            SyncedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToArray();

        return new CloudBootstrap
        {
            User = user,
            Patient = patient,
            Vitals = vitals,
            Medications = medications,
            SleepSessions = sleep,
            EnvironmentReadings = env,
            OcrDocuments = ocr,
            MedicalHistoryEntries = history
        };
    }

    public async Task<User?> GetCurrentUserProfileAsync()
    {
        try
        {
            if (_isOffline) return null;
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
            if (_isOffline) return false;
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
                    Address = user.Address,
                    City = user.City,
                    Country = user.Country,
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
            if (_isOffline) return false;
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
        if (IsCircuitOpen)
            return null;

        try
        {
            if (_isOffline) return null;
            EnsureAuthenticated();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync("/api/mobile/sync/patients/profile", cts.Token);
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
        catch (TaskCanceledException)
        {
            TripCircuit("timeout");
            return null;
        }
        catch (HttpRequestException)
        {
            TripCircuit("network");
            return null;
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
            if (_isOffline) return false;
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
            if (_isOffline) return [];
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
            if (_isOffline) return false;
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
            if (_isOffline) return [];
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
            if (_isOffline) return false;
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
            if (_isOffline) return false;
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
            if (_isOffline) return false;
            EnsureAuthenticated();
            var items = documents.Select(d => new OcrDocumentSyncItem
            {
                Id = d.Id, OpaqueInternalName = d.OpaqueInternalName,
                MimeType = d.MimeType, DocumentType = d.DocumentType, PageCount = d.PageCount,
                Sha256OfNormalized = d.Sha256OfNormalized,
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
            if (_isOffline) return false;
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
        if (string.IsNullOrEmpty(_tokenStore.AccessToken))
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first (Google token missing).");

        // Each typed client gets its own HttpClient instance; ensure header is applied.
        if (_httpClient.DefaultRequestHeaders.Authorization?.Parameter != _tokenStore.AccessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        }
    }

    private static string GetDeviceId()
    {
        return Environment.MachineName ?? "mobile-device";
    }

    // Note: DTOs moved to `CloudSyncDtos.cs` for NativeAOT-safe source generation.

    // ── Doctor assignments (read-only from cloud) ────────────────────────────

    public async Task<IEnumerable<Domain.Models.AssignedDoctor>> GetAssignedDoctorsAsync()
    {
        if (IsCircuitOpen)
            return [];

        if (_isOffline) return [];

        try
        {
            EnsureAuthenticated();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync("/api/mobile/doctors/assigned", cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Doctor assignments fetch failed with status {(int)response.StatusCode} ({response.StatusCode}).");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.AssignedDoctorsResponse);

            if (result == null)
            {
                throw new InvalidOperationException("Doctor assignments response payload was empty.");
            }

            return result.Doctors?.Select(d => new Domain.Models.AssignedDoctor
            {
                DoctorId = d.DoctorId,
                FullName = d.FullName ?? string.Empty,
                Email = d.Email ?? string.Empty,
                PhotoUrl = d.PhotoUrl,
                AssignedAt = d.AssignedAt,
                Notes = d.Notes
            }) ?? [];
        }
        catch (TaskCanceledException)
        {
            TripCircuit("timeout");
            return [];
        }
        catch (HttpRequestException)
        {
            TripCircuit("network");
            return [];
        }
    }

    // ── Notifications (read-only from cloud) ──────────────────────────────────

    public async Task<IEnumerable<DigitalTwin.Mobile.Domain.Models.NotificationItem>> GetNotificationsAsync(int limit = 50, bool unreadOnly = false)
    {
        if (IsCircuitOpen)
        {
            _logger.LogDebug("[CloudSync] Circuit open — returning empty notifications");
            return [];
        }

        if (_isOffline) return [];

        _logger.LogInformation("[CloudDebug][CloudSync] GetNotificationsAsync start limit={Limit} unreadOnly={UnreadOnly} tokenPresent={TokenPresent}",
            limit, unreadOnly, !string.IsNullOrEmpty(_tokenStore.AccessToken));
        try
        {
            EnsureAuthenticated();

            var queryParams = new List<string> { $"limit={limit}" };
            if (unreadOnly)
                queryParams.Add("unreadOnly=true");

            var queryString = "?" + string.Join("&", queryParams);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync($"/api/notifications{queryString}", cts.Token);
        
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CloudSync] Notifications fetch failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            // WebAPI returns a JSON array (not a wrapped object).
            var items = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.ListCloudNotificationItemDto);
            if (items == null) return [];

            return items.Select(n => new DigitalTwin.Mobile.Domain.Models.NotificationItem
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                Type = n.Type,
                Severity = n.Severity,
                RecipientUserId = n.RecipientUserId,
                ActorUserId = n.ActorUserId,
                ActorName = n.ActorName,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            });
        }
        catch (TaskCanceledException)
        {
            TripCircuit("timeout");
            return [];
        }
        catch (HttpRequestException)
        {
            TripCircuit("network");
            return [];
        }
    }

    public async Task<int> GetUnreadNotificationCountAsync()
    {
        if (_isOffline) return 0;

        try
        {
            if (IsCircuitOpen)
                return 0;

            EnsureAuthenticated();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync("/api/notifications/unread-count", cts.Token);
            
            if (!response.IsSuccessStatusCode)
                return 0;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync(stream, InfrastructureJsonContext.Default.UnreadCountResponse);
            return result?.Count ?? 0;
        }
        catch (TaskCanceledException)
        {
            TripCircuit("timeout");
            return 0;
        }
        catch (HttpRequestException)
        {
            TripCircuit("network");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CloudSync] Failed to get unread count");
            return 0;
        }
    }

    public async Task<bool> SendCriticalAlertAsync(CriticalAlertEvent alert)
    {
        if (_isOffline) return false;

        try
        {
            _logger.LogInformation("[CloudDebug][CloudSync] SendCriticalAlertAsync start rule={Rule} tokenPresent={TokenPresent}",
                alert.RuleName, !string.IsNullOrEmpty(_tokenStore.AccessToken));
            EnsureAuthenticated();

            var request = new DeviceRequestEnvelope<CriticalAlertSyncItem>
            {
                DeviceId = GetDeviceId(),
                RequestId = Guid.NewGuid().ToString(),
                Items =
                [
                    new CriticalAlertSyncItem
                    {
                        RuleName = alert.RuleName,
                        Message = alert.Message,
                        Timestamp = alert.Timestamp
                    }
                ]
            };

            using var content = JsonContent.Create(
                request,
                InfrastructureJsonContext.Default.DeviceRequestEnvelopeCriticalAlertSyncItem);

            var response = await _httpClient.PostAsync("/api/mobile/alerts/ecg", content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CloudSync] Critical alert post failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            _logger.LogInformation("[CloudDebug][CloudSync] SendCriticalAlertAsync ok status={Status}", response.StatusCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CloudSync] Failed to post critical alert");
            return false;
        }
    }
}