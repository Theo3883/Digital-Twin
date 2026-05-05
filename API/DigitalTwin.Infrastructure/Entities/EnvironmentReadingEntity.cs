namespace DigitalTwin.Infrastructure.Entities;

public class EnvironmentReadingEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal PM25 { get; set; }
    public decimal PM10 { get; set; }
    public decimal O3 { get; set; }
    public decimal NO2 { get; set; }
    public decimal Temperature { get; set; }
    public decimal Humidity { get; set; }
    public int AirQualityLevel { get; set; }
    public int AqiIndex { get; set; }
    public bool IsDirty { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
