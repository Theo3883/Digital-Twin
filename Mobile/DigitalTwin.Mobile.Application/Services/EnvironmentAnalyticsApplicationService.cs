using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class EnvironmentAnalyticsApplicationService
{
    private readonly IVitalSignRepository _vitalsRepo;
    private readonly IEnvironmentReadingRepository _envRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly ILogger<EnvironmentAnalyticsApplicationService> _logger;

    public EnvironmentAnalyticsApplicationService(
        IVitalSignRepository vitalsRepo,
        IEnvironmentReadingRepository envRepo,
        IPatientRepository patientRepo,
        ILogger<EnvironmentAnalyticsApplicationService> logger)
    {
        _vitalsRepo = vitalsRepo;
        _envRepo = envRepo;
        _patientRepo = patientRepo;
        _logger = logger;
    }

    public async Task<EnvironmentAnalyticsDto> GetLast24HoursAsync()
    {
        try
        {
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient == null)
                return EmptyResult("No patient profile");

            var since = DateTime.UtcNow.AddHours(-24);

            var hrVitals = await _vitalsRepo.GetByTypeAsync(patient.Id, VitalSignType.HeartRate, since, DateTime.UtcNow);
            var envReadings = await _envRepo.GetSinceAsync(since);

            var hrSeries = BucketByHour(hrVitals.Select(v => (v.Timestamp, v.Value)));
            var pm25Series = BucketByHour(envReadings.Select(e => (e.Timestamp, e.PM25)));

            double? correlationR = ComputeCorrelation(hrSeries, pm25Series);
            var footnote = correlationR.HasValue
                ? $"r ≈ {correlationR.Value:F2} — 24h heart rate vs PM2.5 correlation"
                : "Insufficient data for correlation analysis";

            return new EnvironmentAnalyticsDto
            {
                CorrelationR = correlationR,
                Footnote = footnote,
                HeartRateSeries = hrSeries,
                Pm25Series = pm25Series
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnvAnalytics] Failed to compute 24h analytics");
            return EmptyResult("Analytics unavailable");
        }
    }

    private static EnvironmentAnalyticsDto EmptyResult(string footnote) => new()
    {
        Footnote = footnote,
        HeartRateSeries = [],
        Pm25Series = []
    };

    private static HourlyDataPoint[] BucketByHour(IEnumerable<(DateTime Timestamp, double Value)> data)
    {
        return data
            .GroupBy(d => d.Timestamp.Hour)
            .Select(g => new HourlyDataPoint { Hour = g.Key, Value = g.Average(x => x.Value) })
            .OrderBy(p => p.Hour)
            .ToArray();
    }

    private static double? ComputeCorrelation(HourlyDataPoint[] xs, HourlyDataPoint[] ys)
    {
        // Match by hour
        var paired = xs.Join(ys, x => x.Hour, y => y.Hour, (x, y) => (X: x.Value, Y: y.Value)).ToArray();
        if (paired.Length < 3) return null;

        var n = paired.Length;
        var meanX = paired.Average(p => p.X);
        var meanY = paired.Average(p => p.Y);

        var sumXY = paired.Sum(p => (p.X - meanX) * (p.Y - meanY));
        var sumX2 = paired.Sum(p => (p.X - meanX) * (p.X - meanX));
        var sumY2 = paired.Sum(p => (p.Y - meanY) * (p.Y - meanY));

        var denominator = Math.Sqrt(sumX2 * sumY2);
        if (denominator < 1e-10) return null;

        return sumXY / denominator;
    }
}
