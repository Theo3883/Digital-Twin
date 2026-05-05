namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface ICoachingProvider
{
    Task<string> GetAdviceAsync(string patientContext, CancellationToken ct = default);
}
