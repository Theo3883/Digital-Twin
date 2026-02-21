using IOSHealthApp.Domain.Enums;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Services;

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
