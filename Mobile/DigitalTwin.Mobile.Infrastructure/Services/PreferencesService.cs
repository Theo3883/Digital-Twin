using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

/// <summary>
/// In-memory preferences service with line-based file persistence (NativeAOT-safe)
/// Avoids JSON serialization which is reflection-based and incompatible with NativeAOT
/// </summary>
public class PreferencesService : IPreferencesService
{
    private readonly Dictionary<string, string?> _cache = new();
    private readonly string _preferencesFilePath;
    private readonly ILogger<PreferencesService> _logger;
    private bool _isInitialized = false;
    private const string Delimiter = "=";
    private const string NullValue = "__NULL__";

    public PreferencesService(string preferencesFilePath, ILogger<PreferencesService> logger)
    {
        _preferencesFilePath = preferencesFilePath;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        await EnsureInitializedAsync();
        _cache.TryGetValue(key, out var value);
        return value;
    }

    public async Task SetAsync(string key, string? value)
    {
        await EnsureInitializedAsync();
        _cache[key] = value;
        await PersistAsync();
    }

    public async Task RemoveAsync(string key)
    {
        await EnsureInitializedAsync();
        _cache.Remove(key);
        await PersistAsync();
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await EnsureInitializedAsync();
        return _cache.ContainsKey(key);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            if (File.Exists(_preferencesFilePath))
            {
                var lines = await File.ReadAllLinesAsync(_preferencesFilePath);
                _cache.Clear();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains(Delimiter))
                        continue;

                    var parts = line.Split(new[] { Delimiter }, 2, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var key = parts[0];
                        var value = parts[1] == NullValue ? null : parts[1];
                        _cache[key] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PreferencesService] Failed to load preferences from {Path}", _preferencesFilePath);
        }

        _isInitialized = true;
    }

    private async Task PersistAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_preferencesFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = _cache.Select(kvp => 
                $"{kvp.Key}{Delimiter}{(kvp.Value == null ? NullValue : kvp.Value)}"
            ).ToArray();

            await File.WriteAllLinesAsync(_preferencesFilePath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PreferencesService] Failed to persist preferences to {Path}", _preferencesFilePath);
        }
    }
}
