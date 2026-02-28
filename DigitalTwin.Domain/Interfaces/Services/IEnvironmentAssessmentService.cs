using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IEnvironmentAssessmentService
{
    IObservable<RiskEvent> RiskEvents { get; }
    AirQualityLevel DetermineAirQuality(double pm25);
    EnvironmentReading AssessReading(EnvironmentReading reading);
}
