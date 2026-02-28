using System.Reactive.Subjects;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Models;

using DigitalTwin.Domain.Interfaces;

namespace DigitalTwin.Domain.Services;

public class EnvironmentAssessmentService : IEnvironmentAssessmentService
{
    private readonly Subject<RiskEvent> _riskEvents = new();

    public IObservable<RiskEvent> RiskEvents => _riskEvents;

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

        if (reading.AirQuality == AirQualityLevel.Unhealthy)
        {
            _riskEvents.OnNext(new RiskEvent
            {
                AirQualityLevel = reading.AirQuality,
                Message = $"Air quality is unhealthy (PM2.5: {reading.PM25:F1})",
                Timestamp = DateTime.UtcNow
            });
        }

        return reading;
    }
}
