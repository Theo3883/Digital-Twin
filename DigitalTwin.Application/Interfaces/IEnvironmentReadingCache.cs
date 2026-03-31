using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Persists the last environment reading and Gemini advice in app preferences (not the medical DB).
/// </summary>
public interface IEnvironmentReadingCache
{
    EnvironmentReadingDto? GetLastOrDefault();

    void Save(EnvironmentReadingDto reading);

    CoachingAdviceDto? GetLastAdvice();

    void SaveAdvice(CoachingAdviceDto advice);
}
