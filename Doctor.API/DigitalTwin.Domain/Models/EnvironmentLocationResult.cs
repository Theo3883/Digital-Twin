namespace DigitalTwin.Domain.Models;

/// <summary>
/// Resolved coordinates and a label for display (e.g. city name or "Current location").
/// </summary>
public sealed record EnvironmentLocationResult(
    double Latitude,
    double Longitude,
    string DisplayName);
