using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Persists the last environment reading in app preferences (not the medical DB).
/// </summary>
public interface IEnvironmentReadingCache
{
    EnvironmentReadingDto? GetLastOrDefault();

    void Save(EnvironmentReadingDto reading);
}
