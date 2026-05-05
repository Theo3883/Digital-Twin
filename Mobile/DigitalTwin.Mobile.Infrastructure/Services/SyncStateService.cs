using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

/// <summary>
/// Persists sync checkpoint data to track last successful sync for each entity type.
/// Enables incremental/delta sync to avoid excessive data queries.
/// </summary>
public class SyncStateService : ISyncStateService
{
    private const string SyncStateKeyPrefix = "sync_checkpoint_";
    private const int MinimumSyncWindowDays = 30; // Fallback to 30 days if no checkpoint

    private readonly IPreferencesService _preferencesService;
    private readonly ILogger<SyncStateService> _logger;
    private Dictionary<string, DateTime?> _syncStates = new();
    private bool _isLoaded = false;

    public SyncStateService(IPreferencesService preferencesService, ILogger<SyncStateService> logger)
    {
        _preferencesService = preferencesService;
        _logger = logger;
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(string entityType)
    {
        try
        {
            await LoadSyncStatesAsync();
            if (_syncStates.TryGetValue(entityType, out var lastSync))
            {
                _logger.LogDebug("[SyncStateService] Last sync for {EntityType}: {LastSync}", entityType, lastSync);
                return lastSync;
            }

            _logger.LogDebug("[SyncStateService] No prior sync found for {EntityType} - will use fallback window", entityType);
            return null; // Will trigger fallback to minimum window in caller
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncStateService] Failed to load sync time for {EntityType}", entityType);
            return null;
        }
    }

    public async Task SetLastSyncTimeAsync(string entityType, DateTime syncTime)
    {
        try
        {
            await LoadSyncStatesAsync();
            _syncStates[entityType] = syncTime;
            await SaveSyncStatesAsync();
            _logger.LogInformation("[SyncStateService] Updated sync checkpoint for {EntityType}: {SyncTime}", entityType, syncTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncStateService] Failed to save sync time for {EntityType}", entityType);
        }
    }

    public async Task ResetAllCheckpointsAsync()
    {
        try
        {
            await LoadSyncStatesAsync();
            foreach (var key in _syncStates.Keys.ToList())
            {
                await _preferencesService.RemoveAsync(SyncStateKeyPrefix + key);
            }
            _syncStates.Clear();
            _logger.LogInformation("[SyncStateService] Reset all sync checkpoints");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncStateService] Failed to reset checkpoints");
        }
    }

    public async Task<Dictionary<string, DateTime?>> GetAllSyncStatesAsync()
    {
        try
        {
            await LoadSyncStatesAsync();
            return new Dictionary<string, DateTime?>(_syncStates);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncStateService] Failed to load all sync states");
            return new Dictionary<string, DateTime?>();
        }
    }

    private async Task LoadSyncStatesAsync()
    {
        try
        {
            if (_isLoaded)
                return; // Already loaded

            _syncStates = new();

            // Load individual checkpoint keys from preferences
            // This avoids needing to deserialize a complex Dictionary
            var keys = new[] { "Patient", "VitalSigns", "DoctorAssignments" };
            foreach (var key in keys)
            {
                var prefKey = SyncStateKeyPrefix + key;
                var isoString = await _preferencesService.GetAsync(prefKey);
                if (!string.IsNullOrEmpty(isoString) && DateTime.TryParse(isoString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    _syncStates[key] = dt;
                }
                else
                {
                    _syncStates[key] = null;
                }
            }

            _isLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncStateService] Failed to load sync states");
            _syncStates = new();
            _isLoaded = true;
        }
    }

    private async Task SaveSyncStatesAsync()
    {
        try
        {
            foreach (var kvp in _syncStates)
            {
                var prefKey = SyncStateKeyPrefix + kvp.Key;
                if (kvp.Value.HasValue)
                {
                    await _preferencesService.SetAsync(prefKey, kvp.Value.Value.ToString("O")); // ISO 8601 format
                }
                else
                {
                    await _preferencesService.RemoveAsync(prefKey);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncStateService] Failed to save sync states");
        }
    }
}
