using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Models;

public class EnvironmentReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationDisplayName { get; set; } = string.Empty;
    public double PM25 { get; set; }
    public double PM10 { get; set; }
    public double O3 { get; set; }
    public double NO2 { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public AirQualityLevel AirQuality { get; set; }
    public int AqiIndex { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
}
