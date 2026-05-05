using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class EcgApplicationService
{
    private readonly EcgTriageEngine _triageEngine;
    private readonly ILogger<EcgApplicationService> _logger;

    public EcgApplicationService(
        EcgTriageEngine triageEngine,
        ILogger<EcgApplicationService> logger)
    {
        _triageEngine = triageEngine;
        _logger = logger;
    }

    public (EcgFrameDto Frame, CriticalAlertDto? Alert) EvaluateFrame(EcgFrame frame)
    {
        var (result, alert) = _triageEngine.Evaluate(frame);

        var frameDto = new EcgFrameDto
        {
            Samples = frame.Samples,
            SpO2 = frame.SpO2,
            HeartRate = frame.HeartRate,
            Timestamp = frame.Timestamp,
            TriageResult = result.ToString()
        };

        CriticalAlertDto? alertDto = null;
        if (alert != null)
        {
            alertDto = new CriticalAlertDto
            {
                RuleName = alert.RuleName,
                Message = alert.Message,
                Timestamp = alert.Timestamp
            };
            _logger.LogWarning("[EcgApp] Critical alert: {RuleName} — {Message}", alert.RuleName, alert.Message);
        }

        return (frameDto, alertDto);
    }
}
