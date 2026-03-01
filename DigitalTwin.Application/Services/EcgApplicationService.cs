using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Services.Triage;

namespace DigitalTwin.Application.Services;

public class EcgApplicationService : IEcgApplicationService
{
    private readonly IEcgStreamProvider _ecgStreamProvider;
    private readonly EcgTriageEngine _triageEngine;

    public EcgApplicationService(
        IEcgStreamProvider ecgStreamProvider,
        EcgTriageEngine triageEngine)
    {
        _ecgStreamProvider = ecgStreamProvider;
        _triageEngine = triageEngine;
    }

    public IObservable<CriticalAlertEvent> CriticalAlerts => _triageEngine.CriticalAlerts;

    public IObservable<EcgFrameDto> GetEcgStream()
    {
        return _ecgStreamProvider.GetEcgStream()
            .Do(frame => _triageEngine.Evaluate(frame))
            .Select(EcgFrameMapper.ToDto);
    }

    public Task ConnectAsync(string deviceUrl, CancellationToken cancellationToken = default)
        => _ecgStreamProvider.ConnectAsync(deviceUrl, cancellationToken);

    public Task DisconnectAsync()
        => _ecgStreamProvider.DisconnectAsync();
}
