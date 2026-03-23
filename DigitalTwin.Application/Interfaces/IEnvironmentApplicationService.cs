using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines application operations for current and streaming environment data.
/// </summary>
public interface IEnvironmentApplicationService
{
    /// <summary>
    /// Gets the current assessed environment reading.
    /// </summary>
    Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync();

    /// <summary>
    /// Subscribes to assessed environment updates.
    /// </summary>
    IObservable<EnvironmentReadingDto> SubscribeToEnvironment();

    /// <summary>
    /// Gets the stream of generated risk events.
    /// </summary>
    IObservable<RiskEventDto> RiskEvents { get; }
}
