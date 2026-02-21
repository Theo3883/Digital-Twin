namespace DigitalTwin.Infrastructure.Entities;

public class EnvironmentReadingEntity
{
    public long Id { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal PM25 { get; set; }
    public decimal Temperature { get; set; }
    public decimal Humidity { get; set; }
    public int AirQualityLevel { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
