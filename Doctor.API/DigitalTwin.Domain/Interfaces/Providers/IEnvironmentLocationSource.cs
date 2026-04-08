using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

/// <summary>
/// Resolves latitude/longitude for environment API calls from device GPS or manual city name.
/// </summary>
public interface IEnvironmentLocationSource
{
    /// <summary>
    /// Gets the current mode (persisted in app preferences).
    /// </summary>
    EnvironmentLocationMode Mode { get; }

    /// <summary>
    /// Gets the city query for manual mode (persisted).
    /// </summary>
    string? ManualCityName { get; }

    void SetMode(EnvironmentLocationMode mode);

    void SetManualCityName(string? cityName);

    /// <summary>
    /// Resolves coordinates for the next environment fetch. Throws if manual mode has no city or geocoding finds nothing.
    /// </summary>
    Task<EnvironmentLocationResult> ResolveAsync(CancellationToken cancellationToken = default);
}
