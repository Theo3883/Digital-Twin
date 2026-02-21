using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface ICoachingProvider
{
    Task<string> GetAdviceAsync(PatientProfile profile);
}
