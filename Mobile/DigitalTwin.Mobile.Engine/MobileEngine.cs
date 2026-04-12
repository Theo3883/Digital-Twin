using System.Diagnostics;
using System.Text.Json;
using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Application.Services;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Services;
using DigitalTwin.Mobile.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SQLitePCL;

namespace DigitalTwin.Mobile.Engine;

/// <summary>
/// Main engine facade for SwiftUI to interact with the embedded .NET backend.
/// Provides coarse-grained operations and handles all .NET complexity internally.
/// </summary>
public class MobileEngine : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceScope _scope;
    private readonly ILogger<MobileEngine> _logger;

    public MobileEngine(string databasePath, string apiBaseUrl, string? geminiApiKey = null, string? openWeatherApiKey = null, string? googleOAuthClientId = null, string? openRouterApiKey = null, string? openRouterModel = null)
    {
        // Ensure SQLite native provider is configured (NativeAOT/iOS-safe).
        // Force system libsqlite3 provider (prevents attempts to dlopen e_sqlite3).
        try
        {
            raw.SetProvider(new SQLite3Provider_sqlite3());
            raw.FreezeProvider();
        }
        catch
        {
            // Fallback if provider is already frozen or set.
        }
        Batteries_V2.Init();

        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var effectiveApiBaseUrl = ResolveEffectiveApiBaseUrl(apiBaseUrl);
                services.AddMobileServices(databasePath, effectiveApiBaseUrl, geminiApiKey, openWeatherApiKey, googleOAuthClientId, openRouterApiKey, openRouterModel);
            });

        _host = builder.Build();
        _scope = _host.Services.CreateScope();
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<MobileEngine>>();

        var effectiveForLog = ResolveEffectiveApiBaseUrl(apiBaseUrl);
        _logger.LogInformation("[MobileEngine] Initialized with database: {DatabasePath}, API: {ApiBaseUrl}",
            databasePath, effectiveForLog);
    }

    private static string ResolveEffectiveApiBaseUrl(string apiBaseUrlFromSwift)
    {
        var compiledDefault = EngineBuildConfig.DefaultApiBaseUrl;
        if (IsValidAbsoluteHttpUrl(compiledDefault))
            return compiledDefault;

        if (IsValidAbsoluteHttpUrl(apiBaseUrlFromSwift))
            return apiBaseUrlFromSwift;

        return "";
    }

    private static bool IsValidAbsoluteHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && !string.IsNullOrWhiteSpace(uri.Host);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Authentication
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Authenticates user with Google ID token
    /// </summary>
    public async Task<string> AuthenticateAsync(string googleIdToken)
    {
        try
        {
            var authService = _scope.ServiceProvider.GetRequiredService<AuthService>();
            var result = await authService.AuthenticateWithGoogleAsync(googleIdToken);
            
            return JsonSerializer.Serialize(result, MobileJsonContext.Default.AuthenticationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Authentication failed");
            var errorResult = new AuthenticationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
            return JsonSerializer.Serialize(errorResult, MobileJsonContext.Default.AuthenticationResult);
        }
    }

    /// <summary>
    /// Gets current authenticated user
    /// </summary>
    public async Task<string> GetCurrentUserAsync()
    {
        try
        {
            var authService = _scope.ServiceProvider.GetRequiredService<AuthService>();
            var user = await authService.GetCurrentUserAsync();
            
            return JsonSerializer.Serialize(user, MobileJsonContext.Default.UserDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get current user");
            return JsonSerializer.Serialize((UserDto?)null, MobileJsonContext.Default.UserDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Patient Profile
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets current patient profile
    /// </summary>
    public async Task<string> GetPatientProfileAsync()
    {
        try
        {
            var patientService = _scope.ServiceProvider.GetRequiredService<PatientService>();
            var patient = await patientService.GetCurrentPatientAsync();
            
            return JsonSerializer.Serialize(patient, MobileJsonContext.Default.PatientDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get patient profile");
            return JsonSerializer.Serialize((PatientDto?)null, MobileJsonContext.Default.PatientDto);
        }
    }

    /// <summary>
    /// Updates patient profile
    /// </summary>
    public async Task<string> UpdatePatientProfileAsync(string updateJson)
    {
        try
        {
            var update = JsonSerializer.Deserialize(updateJson, MobileJsonContext.Default.PatientUpdateInput);
            if (update == null)
                throw new ArgumentException("Invalid update data");

            var patientService = _scope.ServiceProvider.GetRequiredService<PatientService>();
            var success = await patientService.UpdatePatientAsync(update);
            
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = success }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to update patient profile");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Vital Signs
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a single vital sign reading
    /// </summary>
    public async Task<string> RecordVitalSignAsync(string vitalSignJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(vitalSignJson, MobileJsonContext.Default.VitalSignInput);
            if (input == null)
                throw new ArgumentException("Invalid vital sign data");

            var vitalsService = _scope.ServiceProvider.GetRequiredService<VitalSignsService>();
            var success = await vitalsService.RecordVitalSignAsync(input);
            
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = success }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to record vital sign");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    /// <summary>
    /// Records multiple vital sign readings (e.g., from HealthKit)
    /// </summary>
    public async Task<string> RecordVitalSignsAsync(string vitalSignsJson)
    {
        try
        {
            var inputs = JsonSerializer.Deserialize(vitalSignsJson, MobileJsonContext.Default.VitalSignInputArray);
            if (inputs == null)
                throw new ArgumentException("Invalid vital signs data");

            var vitalsService = _scope.ServiceProvider.GetRequiredService<VitalSignsService>();
            var count = await vitalsService.RecordVitalSignsAsync(inputs);
            
            return JsonSerializer.Serialize(new NativeBridge.RecordVitalSignsResultDto { Success = true, Count = count }, MobileJsonContext.Default.RecordVitalSignsResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to record vital signs");
            return JsonSerializer.Serialize(new NativeBridge.RecordVitalSignsResultDto { Success = false, Error = ex.Message, Count = 0 }, MobileJsonContext.Default.RecordVitalSignsResultDto);
        }
    }

    /// <summary>
    /// Gets vital signs for a date range
    /// </summary>
    public async Task<string> GetVitalSignsAsync(string? fromDateIso = null, string? toDateIso = null)
    {
        try
        {
            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (!string.IsNullOrEmpty(fromDateIso))
                fromDate = DateTime.Parse(fromDateIso);
            
            if (!string.IsNullOrEmpty(toDateIso))
                toDate = DateTime.Parse(toDateIso);

            var vitalsService = _scope.ServiceProvider.GetRequiredService<VitalSignsService>();
            var vitals = await vitalsService.GetVitalSignsAsync(fromDate, toDate);
            
            return JsonSerializer.Serialize(vitals.ToArray(), MobileJsonContext.Default.VitalSignDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get vital signs");
            return JsonSerializer.Serialize(Array.Empty<VitalSignDto>(), MobileJsonContext.Default.VitalSignDtoArray);
        }
    }

    /// <summary>
    /// Gets vital signs by type
    /// </summary>
    public async Task<string> GetVitalSignsByTypeAsync(int vitalTypeInt, string? fromDateIso = null, string? toDateIso = null)
    {
        try
        {
            var vitalType = (VitalSignType)vitalTypeInt;
            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (!string.IsNullOrEmpty(fromDateIso))
                fromDate = DateTime.Parse(fromDateIso);
            
            if (!string.IsNullOrEmpty(toDateIso))
                toDate = DateTime.Parse(toDateIso);

            var vitalsService = _scope.ServiceProvider.GetRequiredService<VitalSignsService>();
            var vitals = await vitalsService.GetVitalSignsByTypeAsync(vitalType, fromDate, toDate);
            
            return JsonSerializer.Serialize(vitals.ToArray(), MobileJsonContext.Default.VitalSignDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get vital signs by type");
            return JsonSerializer.Serialize(Array.Empty<VitalSignDto>(), MobileJsonContext.Default.VitalSignDtoArray);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Synchronization
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Performs full bidirectional sync with cloud
    /// </summary>
    public async Task<string> PerformSyncAsync()
    {
        try
        {
            var syncService = _scope.ServiceProvider.GetRequiredService<SyncService>();
            var success = await syncService.PerformFullSyncAsync();
            
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = success }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Sync failed");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    /// <summary>
    /// Pushes local changes to cloud (one-way sync)
    /// </summary>
    public async Task<string> PushLocalChangesAsync()
    {
        try
        {
            var syncService = _scope.ServiceProvider.GetRequiredService<SyncService>();
            await syncService.PushLocalChangesAsync();
            
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = true }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Push failed");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initializes the database (call once on app startup)
    /// </summary>
    public async Task<string> InitializeDatabaseAsync()
    {
        try
        {
            var dbInitializer = _scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await dbInitializer.InitializeAsync();
            
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = true }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Database initialization failed");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _host?.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Medications
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetMedicationsAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<MedicationApplicationService>();
            var meds = await service.GetMedicationsAsync();
            return JsonSerializer.Serialize(meds.ToArray(), MobileJsonContext.Default.MedicationDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get medications");
            return JsonSerializer.Serialize(Array.Empty<MedicationDto>(), MobileJsonContext.Default.MedicationDtoArray);
        }
    }

    public async Task<string> AddMedicationAsync(string inputJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(inputJson, MobileJsonContext.Default.AddMedicationInput);
            if (input == null) throw new ArgumentException("Invalid medication input");

            var service = _scope.ServiceProvider.GetRequiredService<MedicationApplicationService>();
            var (success, error) = await service.AddMedicationAsync(input);

            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = success, Error = error }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to add medication");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    public async Task<string> DiscontinueMedicationAsync(string inputJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(inputJson, MobileJsonContext.Default.DiscontinueMedicationInput);
            if (input == null) throw new ArgumentException("Invalid discontinue input");

            var service = _scope.ServiceProvider.GetRequiredService<MedicationApplicationService>();
            var (success, error) = await service.DiscontinueMedicationAsync(input);

            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = success, Error = error }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to discontinue medication");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    public async Task<string> SearchDrugsAsync(string query)
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<MedicationApplicationService>();
            var results = await service.SearchDrugsAsync(query);
            return JsonSerializer.Serialize(results.ToArray(), MobileJsonContext.Default.DrugSearchResultDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Drug search failed");
            return JsonSerializer.Serialize(Array.Empty<DrugSearchResultDto>(), MobileJsonContext.Default.DrugSearchResultDtoArray);
        }
    }

    public async Task<string> CheckInteractionsAsync(string rxCuisJson)
    {
        try
        {
            var rxCuis = JsonSerializer.Deserialize(rxCuisJson, MobileJsonContext.Default.StringArray);
            if (rxCuis == null) throw new ArgumentException("Invalid RxCUI list");

            var service = _scope.ServiceProvider.GetRequiredService<MedicationApplicationService>();
            var interactions = await service.CheckInteractionsAsync(rxCuis);
            return JsonSerializer.Serialize(interactions.ToArray(), MobileJsonContext.Default.MedicationInteractionDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Interaction check failed");
            return JsonSerializer.Serialize(Array.Empty<MedicationInteractionDto>(), MobileJsonContext.Default.MedicationInteractionDtoArray);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetEnvironmentReadingAsync(double latitude, double longitude)
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<EnvironmentApplicationService>();
            var reading = await service.GetCurrentEnvironmentAsync(latitude, longitude);
            return JsonSerializer.Serialize(reading, MobileJsonContext.Default.EnvironmentReadingDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get environment reading");
            return JsonSerializer.Serialize((EnvironmentReadingDto?)null, MobileJsonContext.Default.EnvironmentReadingDto);
        }
    }

    public async Task<string> GetLatestEnvironmentReadingAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<EnvironmentApplicationService>();
            var reading = await service.GetLatestCachedAsync();
            return JsonSerializer.Serialize(reading, MobileJsonContext.Default.EnvironmentReadingDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get latest environment reading");
            return JsonSerializer.Serialize((EnvironmentReadingDto?)null, MobileJsonContext.Default.EnvironmentReadingDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ECG Triage
    // ═══════════════════════════════════════════════════════════════════════════

    public string EvaluateEcgFrame(string frameJson)
    {
        try
        {
            var frame = JsonSerializer.Deserialize(frameJson, MobileJsonContext.Default.EcgFrameInput);
            if (frame == null) throw new ArgumentException("Invalid ECG frame");

            var ecgFrame = new EcgFrame
            {
                Samples = frame.Samples,
                SpO2 = frame.SpO2,
                HeartRate = frame.HeartRate,
                Timestamp = frame.Timestamp
            };

            var service = _scope.ServiceProvider.GetRequiredService<EcgApplicationService>();
            var (frameDto, alertDto) = service.EvaluateFrame(ecgFrame);

            var result = new EcgEvaluationResult { Frame = frameDto, Alert = alertDto };
            return JsonSerializer.Serialize(result, MobileJsonContext.Default.EcgEvaluationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] ECG evaluation failed");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  AI Chat
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> SendChatMessageAsync(string message)
    {
        using var activity = new Activity("MobileEngine.SendChatMessage");
        activity.Start();
        var correlationId = activity.TraceId.ToString();

        try
        {
            _logger.LogInformation(
                "[MobileEngine][{CorrelationId}] Sending chat message ({Length} chars).",
                correlationId,
                message.Length);

            var service = _scope.ServiceProvider.GetRequiredService<ChatBotApplicationService>();
            var response = await service.SendMessageAsync(message);

            _logger.LogInformation(
                "[MobileEngine][{CorrelationId}] Chat message completed ({Length} chars).",
                correlationId,
                response.Content.Length);

            return JsonSerializer.Serialize(response, MobileJsonContext.Default.ChatMessageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine][{CorrelationId}] Chat message failed", correlationId);
            var errorDto = new ChatMessageDto
            {
                Content = "An error occurred. Please try again.",
                IsUser = false,
                Timestamp = DateTime.UtcNow
            };
            return JsonSerializer.Serialize(errorDto, MobileJsonContext.Default.ChatMessageDto);
        }
    }

    public async Task<string> GetChatHistoryAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<ChatBotApplicationService>();
            var history = await service.GetChatHistoryAsync();
            return JsonSerializer.Serialize(history.ToArray(), MobileJsonContext.Default.ChatMessageDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get chat history");
            return JsonSerializer.Serialize(Array.Empty<ChatMessageDto>(), MobileJsonContext.Default.ChatMessageDtoArray);
        }
    }

    public async Task<string> ClearChatHistoryAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<ChatBotApplicationService>();
            await service.ClearChatHistoryAsync();
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = true }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to clear chat history");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Coaching
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetCoachingAdviceAsync()
    {
        using var activity = new Activity("MobileEngine.GetCoachingAdvice");
        activity.Start();
        var correlationId = activity.TraceId.ToString();

        try
        {
            _logger.LogInformation("[MobileEngine][{CorrelationId}] Fetching coaching advice.", correlationId);

            var service = _scope.ServiceProvider.GetRequiredService<CoachingApplicationService>();
            var advice = await service.GetAdviceAsync();

            _logger.LogInformation(
                "[MobileEngine][{CorrelationId}] Coaching advice fetched ({Length} chars).",
                correlationId,
                advice.Advice.Length);

            return JsonSerializer.Serialize(advice, MobileJsonContext.Default.CoachingAdviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine][{CorrelationId}] Failed to get coaching advice", correlationId);
            var fallback = new CoachingAdviceDto
            {
                Advice = "Stay hydrated, get regular exercise, and maintain a balanced diet.",
                Timestamp = DateTime.UtcNow
            };
            return JsonSerializer.Serialize(fallback, MobileJsonContext.Default.CoachingAdviceDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Doctor Assignments
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetAssignedDoctorsAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<DoctorAssignmentApplicationService>();
            var doctors = await service.GetAssignedDoctorsAsync();
            return JsonSerializer.Serialize(doctors, MobileJsonContext.Default.AssignedDoctorDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get assigned doctors");
            return JsonSerializer.Serialize(Array.Empty<AssignedDoctorDto>(), MobileJsonContext.Default.AssignedDoctorDtoArray);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Local Data Reset
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> ResetLocalDataAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<Domain.Interfaces.ILocalDataResetService>();
            await service.ResetAllAsync();
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = true }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to reset local data");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment Analytics
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetEnvironmentAnalyticsAsync()
    {
        try
        {
            var service = _scope.ServiceProvider.GetRequiredService<EnvironmentAnalyticsApplicationService>();
            var analytics = await service.GetLast24HoursAsync();
            return JsonSerializer.Serialize(analytics, MobileJsonContext.Default.EnvironmentAnalyticsDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get environment analytics");
            return JsonSerializer.Serialize(new EnvironmentAnalyticsDto { Footnote = "Analytics unavailable" }, MobileJsonContext.Default.EnvironmentAnalyticsDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment Coaching Advice
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetEnvironmentAdviceAsync()
    {
        try
        {
            var coaching = _scope.ServiceProvider.GetRequiredService<CoachingApplicationService>();
            var envService = _scope.ServiceProvider.GetRequiredService<EnvironmentApplicationService>();

            var reading = await envService.GetLatestCachedAsync();
            if (reading == null)
            {
                var fallback = new CoachingAdviceDto
                {
                    Advice = "Get an environment reading first to receive personalized advice.",
                    Timestamp = DateTime.UtcNow
                };
                return JsonSerializer.Serialize(fallback, MobileJsonContext.Default.CoachingAdviceDto);
            }

            var advice = await coaching.GetEnvironmentAdviceAsync(reading);
            return JsonSerializer.Serialize(advice, MobileJsonContext.Default.CoachingAdviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get environment advice");
            var fallback = new CoachingAdviceDto
            {
                Advice = "Monitor your environment regularly for health impacts.",
                Timestamp = DateTime.UtcNow
            };
            return JsonSerializer.Serialize(fallback, MobileJsonContext.Default.CoachingAdviceDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Sleep
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> RecordSleepSessionAsync(string sessionJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(sessionJson, MobileJsonContext.Default.SleepSessionInput);
            if (input == null) throw new ArgumentException("Invalid sleep session input");

            var service = _scope.ServiceProvider.GetRequiredService<SleepApplicationService>();
            var success = await service.RecordSleepSessionAsync(input);
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = success }, MobileJsonContext.Default.OperationResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to record sleep session");
            return JsonSerializer.Serialize(new NativeBridge.OperationResultDto { Success = false, Error = ex.Message }, MobileJsonContext.Default.OperationResultDto);
        }
    }

    public async Task<string> GetSleepSessionsAsync(string? fromDateIso = null, string? toDateIso = null)
    {
        try
        {
            DateTime? from = null, to = null;
            if (!string.IsNullOrEmpty(fromDateIso)) from = DateTime.Parse(fromDateIso);
            if (!string.IsNullOrEmpty(toDateIso)) to = DateTime.Parse(toDateIso);

            var service = _scope.ServiceProvider.GetRequiredService<SleepApplicationService>();
            var sessions = await service.GetSleepSessionsAsync(from, to);
            return JsonSerializer.Serialize(sessions.ToArray(), MobileJsonContext.Default.SleepSessionDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get sleep sessions");
            return JsonSerializer.Serialize(Array.Empty<SleepSessionDto>(), MobileJsonContext.Default.SleepSessionDtoArray);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Medical History
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetMedicalHistoryAsync()
    {
        try
        {
            var patientRepo = _scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IPatientRepository>();
            var historyRepo = _scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IMedicalHistoryEntryRepository>();

            var patient = await patientRepo.GetCurrentPatientAsync();
            if (patient == null)
                return JsonSerializer.Serialize(Array.Empty<MedicalHistoryEntryDto>(), MobileJsonContext.Default.MedicalHistoryEntryDtoArray);

            var entries = await historyRepo.GetByPatientIdAsync(patient.Id);
            var dtos = entries.Select(e => new MedicalHistoryEntryDto
            {
                Id = e.Id,
                SourceDocumentId = e.SourceDocumentId,
                Title = e.Title,
                MedicationName = e.MedicationName,
                Dosage = e.Dosage,
                Frequency = e.Frequency,
                Duration = e.Duration,
                Notes = e.Notes,
                Summary = e.Summary,
                Confidence = e.Confidence,
                EventDate = e.EventDate
            }).ToArray();

            return JsonSerializer.Serialize(dtos, MobileJsonContext.Default.MedicalHistoryEntryDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get medical history");
            return JsonSerializer.Serialize(Array.Empty<MedicalHistoryEntryDto>(), MobileJsonContext.Default.MedicalHistoryEntryDtoArray);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  OCR Documents
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetOcrDocumentsAsync()
    {
        try
        {
            var patientRepo = _scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IPatientRepository>();
            var ocrRepo = _scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IOcrDocumentRepository>();

            var patient = await patientRepo.GetCurrentPatientAsync();
            if (patient == null)
                return JsonSerializer.Serialize(Array.Empty<OcrDocumentDto>(), MobileJsonContext.Default.OcrDocumentDtoArray);

            var docs = await ocrRepo.GetByPatientIdAsync(patient.Id);
            var dtos = docs.Select(d => new OcrDocumentDto
            {
                Id = d.Id,
                PatientId = patient.Id,
                OpaqueInternalName = d.OpaqueInternalName,
                MimeType = d.MimeType ?? string.Empty,
                DocumentType = d.DocumentType ?? "Unknown",
                PageCount = d.PageCount,
                Sha256OfNormalized = d.Sha256OfNormalized ?? string.Empty,
                EncryptedVaultPath = d.EncryptedVaultPath ?? string.Empty,
                SanitizedOcrPreview = d.SanitizedOcrPreview ?? string.Empty,
                ScannedAt = d.ScannedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                IsDirty = d.IsDirty,
                SyncedAt = d.SyncedAt
            }).ToArray();

            return JsonSerializer.Serialize(dtos, MobileJsonContext.Default.OcrDocumentDtoArray);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to get OCR documents");
            return JsonSerializer.Serialize(Array.Empty<OcrDocumentDto>(), MobileJsonContext.Default.OcrDocumentDtoArray);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  OCR Text Processing
    // ═══════════════════════════════════════════════════════════════════════════

    public string ClassifyDocument(string ocrText)
    {
        try
        {
            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            return svc.ClassifyDocument(ocrText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to classify document");
            return "Unknown";
        }
    }

    public string ExtractIdentity(string ocrText)
    {
        try
        {
            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            var identity = svc.ExtractIdentity(ocrText);
            return JsonSerializer.Serialize(identity, MobileJsonContext.Default.DocumentIdentity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to extract identity");
            return JsonSerializer.Serialize(new Domain.Models.DocumentIdentity(null, null, 0f, 0f), MobileJsonContext.Default.DocumentIdentity);
        }
    }

    public async Task<string> ValidateIdentityAsync(string ocrText)
    {
        try
        {
            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            var result = await svc.ValidateIdentityAsync(ocrText);
            return JsonSerializer.Serialize(result, MobileJsonContext.Default.IdentityValidationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to validate identity");
            return JsonSerializer.Serialize(
                new Domain.Models.IdentityValidationResult(false, false, false, ex.Message),
                MobileJsonContext.Default.IdentityValidationResult);
        }
    }

    public string SanitizeText(string ocrText)
    {
        try
        {
            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            return svc.SanitizeText(ocrText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to sanitize text");
            return ocrText;
        }
    }

    public string ExtractStructured(string ocrText, string documentType)
    {
        try
        {
            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            var result = svc.ExtractStructured(ocrText, documentType);
            return JsonSerializer.Serialize(result, MobileJsonContext.Default.HeuristicExtractionResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to extract structured fields");
            return JsonSerializer.Serialize(Domain.Models.HeuristicExtractionResult.Empty, MobileJsonContext.Default.HeuristicExtractionResult);
        }
    }

    public async Task<string> ProcessFullOcrAsync(string ocrText)
    {
        try
        {
            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            var result = await svc.ProcessFullAsync(ocrText);
            return JsonSerializer.Serialize(result, MobileJsonContext.Default.OcrTextProcessingResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed full OCR processing");
            return JsonSerializer.Serialize(
                new Domain.Models.OcrTextProcessingResult("Unknown", null, null, ocrText, null, []),
                MobileJsonContext.Default.OcrTextProcessingResult);
        }
    }

    public async Task<string> SaveOcrDocumentAsync(string inputJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(inputJson, MobileJsonContext.Default.SaveOcrDocumentInput)
                        ?? throw new ArgumentException("Invalid input JSON");
            if (!string.IsNullOrWhiteSpace(input.CachedProcessingResultJson))
            {
                var parsed = JsonSerializer.Deserialize(
                    input.CachedProcessingResultJson,
                    MobileJsonContext.Default.OcrTextProcessingResult);
                input = input with { CachedProcessingResult = parsed };
            }

            var svc = _scope.ServiceProvider.GetRequiredService<Application.Services.OcrTextProcessingApplicationService>();
            var dto = await svc.SaveDocumentFromCaptureAsync(input);
            return JsonSerializer.Serialize(dto, MobileJsonContext.Default.OcrDocumentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] Failed to save OCR document");
            return JsonSerializer.Serialize(
                new NativeBridge.OperationResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.OperationResultDto);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Advanced OCR — Vault, Encryption, Structured Extraction
    // ═══════════════════════════════════════════════════════════════════════════

    public string VaultInitialize(string inputJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(inputJson, MobileJsonContext.Default.VaultInitInput)
                        ?? throw new ArgumentException("Invalid input JSON");
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            var posture = new OCR.Models.SecurityPosture(
                input.IsPasscodeSet, input.IsBiometryAvailable, input.BiometryType,
                input.IsVaultInitialized, input.IsVaultUnlocked,
                Enum.TryParse<OCR.Models.Enums.SecurityMode>(input.ActiveMode, out var mode)
                    ? mode : OCR.Models.Enums.SecurityMode.Strict);
            var result = vault.Initialize(posture);
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = result.IsSuccess, Error = result.Error },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultInitialize failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public string VaultUnlock(string masterKeyBase64)
    {
        try
        {
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            var masterKey = Convert.FromBase64String(masterKeyBase64);
            var result = vault.Unlock(masterKey);
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = result.IsSuccess, Error = result.Error },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultUnlock failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public string VaultLock()
    {
        try
        {
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            vault.Lock();
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = true },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultLock failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public async Task<string> VaultStoreDocumentAsync(string inputJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(inputJson, MobileJsonContext.Default.VaultStoreInput)
                        ?? throw new ArgumentException("Invalid input JSON");
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            var docBytes = Convert.FromBase64String(input.DocumentBase64);
            var docId = Guid.Parse(input.DocumentId);
            var result = await vault.StoreDocumentAsync(docBytes, input.MimeType, input.PageCount, docId);
            if (!result.IsSuccess)
                return JsonSerializer.Serialize(
                    new VaultResultDto { Success = false, Error = result.Error },
                    MobileJsonContext.Default.VaultResultDto);
            return JsonSerializer.Serialize(
                new VaultResultDto
                {
                    Success = true,
                    DocumentId = result.Value!.DocumentId.ToString(),
                    VaultPath = result.Value.VaultPath,
                    Sha256 = result.Value.Sha256OfNormalized,
                    OpaqueInternalName = result.Value.OpaqueInternalName
                },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultStoreDocument failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public async Task<string> VaultRetrieveDocumentAsync(string documentId)
    {
        try
        {
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            var result = await vault.RetrieveDocumentAsync(Guid.Parse(documentId));
            if (!result.IsSuccess)
                return JsonSerializer.Serialize(
                    new VaultResultDto { Success = false, Error = result.Error },
                    MobileJsonContext.Default.VaultResultDto);
            return Convert.ToBase64String(result.Value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultRetrieveDocument failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public string VaultDeleteDocument(string documentId)
    {
        try
        {
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            var result = vault.DeleteDocument(Guid.Parse(documentId));
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = result.IsSuccess, Error = result.Error },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultDeleteDocument failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public string VaultWipe()
    {
        try
        {
            var vault = _scope.ServiceProvider.GetRequiredService<OCR.Services.VaultService>();
            vault.Wipe();
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = true },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] VaultWipe failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }

    public string ClassifyWithOrchestrator(string ocrText, string? mlType, float mlConfidence)
    {
        try
        {
            var orchestrator = _scope.ServiceProvider.GetRequiredService<OCR.Services.ML.ClassificationOrchestrator>();
            var result = orchestrator.Classify(ocrText, mlType, mlConfidence);
            return JsonSerializer.Serialize(
                new ClassifyResultDto { Type = result.Type, Confidence = result.Confidence, Method = result.Method },
                MobileJsonContext.Default.ClassifyResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] ClassifyWithOrchestrator failed");
            return JsonSerializer.Serialize(
                new ClassifyResultDto { Type = "Unknown", Confidence = 0f, Method = "error" },
                MobileJsonContext.Default.ClassifyResultDto);
        }
    }

    public string BuildStructuredDocument(string ocrText, string docType, float classConfidence, string classMethod)
    {
        try
        {
            var builder = _scope.ServiceProvider.GetRequiredService<OCR.Services.StructuredDocumentBuilder>();
            var doc = builder.Build(
                Guid.NewGuid(), ocrText, docType, classConfidence, classMethod,
                graph: null, ocrDuration: TimeSpan.Zero, classificationDuration: TimeSpan.Zero, useMlExtraction: false);
            return JsonSerializer.Serialize(doc, MobileJsonContext.Default.StructuredMedicalDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] BuildStructuredDocument failed");
            return JsonSerializer.Serialize(
                new NativeBridge.OperationResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.OperationResultDto);
        }
    }

    /// <summary>Structured document with stable document id, optional ML extraction, and audit record (MAUI parity).</summary>
    public string BuildStructuredDocumentFromJson(string inputJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize(inputJson, MobileJsonContext.Default.BuildStructuredDocumentInput)
                        ?? throw new ArgumentException("Invalid JSON");
            var docId = Guid.TryParse(input.DocumentId, out var parsed) ? parsed : Guid.NewGuid();
            var builder = _scope.ServiceProvider.GetRequiredService<OCR.Services.StructuredDocumentBuilder>();
            var ocrDuration = TimeSpan.FromMilliseconds(Math.Max(0, input.OcrDurationMs));
            var classDuration = TimeSpan.FromMilliseconds(Math.Max(0, input.ClassificationDurationMs));
            var doc = builder.Build(
                docId, input.OcrText, input.DocType, input.ClassConfidence, input.ClassMethod,
                graph: null, ocrDuration, classDuration, useMlExtraction: input.UseMlExtraction);

            var audit = _scope.ServiceProvider.GetRequiredService<OCR.Services.ML.MlPipelineAuditService>();
            audit.Record(new OCR.Models.ML.MlAuditRecord(
                DocumentId: docId,
                PredictedType: doc.DocumentType,
                ClassificationConfidence: input.ClassConfidence,
                ClassificationMethod: input.ClassMethod,
                ModelVersion: "v1",
                TokenCount: doc.Metrics?.TotalTokens ?? 0,
                BertUsed: doc.PrimaryExtractionMethod == OCR.Models.Structured.ExtractionMethod.MlBertTokenClassifier,
                OcrDuration: ocrDuration,
                ClassificationDuration: classDuration,
                ExtractionDuration: doc.Metrics?.ExtractionDuration ?? TimeSpan.Zero,
                ReviewFlagCount: doc.ReviewFlags?.Count ?? 0,
                RecordedAt: DateTime.UtcNow));

            return JsonSerializer.Serialize(doc, MobileJsonContext.Default.StructuredMedicalDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] BuildStructuredDocumentFromJson failed");
            return JsonSerializer.Serialize(
                new NativeBridge.OperationResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.OperationResultDto);
        }
    }

    public string GetMlAuditSummary()
    {
        try
        {
            var audit = _scope.ServiceProvider.GetRequiredService<OCR.Services.ML.MlPipelineAuditService>();
            var summary = audit.GetSummary();
            return JsonSerializer.Serialize(
                new MlAuditSummaryDto
                {
                    TotalDocuments = summary.TotalDocuments,
                    AverageOcrMs = summary.AverageOcrMs,
                    AverageClassifyMs = summary.AverageClassifyMs,
                    AverageExtractMs = summary.AverageExtractMs,
                    AverageConfidence = summary.AverageConfidence,
                    BertUsagePercent = summary.BertUsagePercent
                },
                MobileJsonContext.Default.MlAuditSummaryDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] GetMlAuditSummary failed");
            return JsonSerializer.Serialize(new MlAuditSummaryDto(), MobileJsonContext.Default.MlAuditSummaryDto);
        }
    }

    public string ValidateDocument(string headerBase64, string fileExtension, long fileSizeBytes)
    {
        try
        {
            var header = Convert.FromBase64String(headerBase64);
            var (isValid, reason) = OCR.Policies.DocumentValidationPolicy.Validate(header, fileExtension, fileSizeBytes);
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = isValid, Error = reason },
                MobileJsonContext.Default.VaultResultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileEngine] ValidateDocument failed");
            return JsonSerializer.Serialize(
                new VaultResultDto { Success = false, Error = ex.Message },
                MobileJsonContext.Default.VaultResultDto);
        }
    }
}