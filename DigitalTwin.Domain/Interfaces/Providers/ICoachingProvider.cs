using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface ICoachingProvider
{
    Task<string> GetAdviceAsync(PatientProfile profile);
}
