namespace DigitalTwin.Domain.Enums;

/// <summary>
/// Whether environment weather/air quality uses GPS or a user-entered city (geocoded).
/// </summary>
public enum EnvironmentLocationMode
{
    Device,
    Manual
}
