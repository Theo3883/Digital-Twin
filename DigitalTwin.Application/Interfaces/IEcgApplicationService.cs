using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Events;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines ECG streaming and connection lifecycle operations.
/// </summary>
public interface IEcgApplicationService
{
    /// <summary>
    /// Gets the live ECG frame stream.
    /// </summary>
    IObservable<EcgFrameDto> GetEcgStream();

    /// <summary>
    /// Gets the observable stream of critical ECG alerts.
    /// </summary>
    IObservable<CriticalAlertEvent> CriticalAlerts { get; }

    /// <summary>
    /// Connects to an ECG device stream.
    /// </summary>
    Task ConnectAsync(string deviceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the ECG device stream.
    /// </summary>
    Task DisconnectAsync();
}
