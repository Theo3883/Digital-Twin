using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Events;

namespace DigitalTwin.Application.Interfaces;

public interface IEcgApplicationService
{
    IObservable<EcgFrameDto> GetEcgStream();

    IObservable<CriticalAlertEvent> CriticalAlerts { get; }

    Task ConnectAsync(string deviceUrl, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
