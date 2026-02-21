using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Interfaces;

public interface ICoachingProvider
{
    Task<string> GetAdviceAsync(PatientProfile profile);
}
