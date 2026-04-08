using System.Text.Json;
using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Application.Services;
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

    public MobileEngine(string databasePath, string apiBaseUrl)
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
                services.AddMobileServices(databasePath, apiBaseUrl);
            });

        _host = builder.Build();
        _scope = _host.Services.CreateScope();
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<MobileEngine>>();

        _logger.LogInformation("[MobileEngine] Initialized with database: {DatabasePath}, API: {ApiBaseUrl}", 
            databasePath, apiBaseUrl);
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
}