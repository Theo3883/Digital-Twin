using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class EnvironmentAssessmentService
{
    public AirQualityLevel DetermineAirQuality(double pm25)
    {
        return pm25 switch
        {
            <= 50 => AirQualityLevel.Good,
            <= 100 => AirQualityLevel.Moderate,
            _ => AirQualityLevel.Unhealthy
        };
    }

    public EnvironmentReading AssessReading(EnvironmentReading reading)
    {
        reading.AirQuality = DetermineAirQuality(reading.PM25);
        return reading;
    }
}
