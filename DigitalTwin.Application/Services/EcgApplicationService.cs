using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Services.Triage;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Connects ECG streaming infrastructure with DTO mapping and triage alert evaluation.
/// </summary>
public class EcgApplicationService : IEcgApplicationService
{
    private readonly IEcgStreamProvider _ecgStreamProvider;
    private readonly EcgTriageEngine _triageEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="EcgApplicationService"/> class.
    /// </summary>
    public EcgApplicationService(
        IEcgStreamProvider ecgStreamProvider,
        EcgTriageEngine triageEngine)
    {
        _ecgStreamProvider = ecgStreamProvider;
        _triageEngine = triageEngine;
    }

    /// <summary>
    /// Gets the observable stream of critical alerts produced by the triage engine.
    /// </summary>
    public IObservable<CriticalAlertEvent> CriticalAlerts => _triageEngine.CriticalAlerts;

    /// <summary>
    /// Gets the live ECG stream while forwarding each frame through triage evaluation.
    /// </summary>
    public IObservable<EcgFrameDto> GetEcgStream()
    {
        return _ecgStreamProvider.GetEcgStream()
            .Do(frame => _triageEngine.Evaluate(frame))
            .Select(EcgFrameMapper.ToDto);
    }

    /// <summary>
    /// Connects to the configured ECG device stream.
    /// </summary>
    public Task ConnectAsync(string deviceUrl, CancellationToken cancellationToken = default)
        => _ecgStreamProvider.ConnectAsync(deviceUrl, cancellationToken);

    /// <summary>
    /// Disconnects from the ECG device stream.
    /// </summary>
    public Task DisconnectAsync()
        => _ecgStreamProvider.DisconnectAsync();
}
