using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Hourly buckets for last 24h: PM2.5 from persisted environment readings, heart rate from persisted vitals.
/// </summary>
public sealed class EnvironmentHealthAnalyticsService : IEnvironmentHealthAnalyticsService
{
    private const int Buckets = 24;
    private const int MinPairsForCorrelation = 8;

    private readonly IEnvironmentReadingRepository _environmentReadings;
    private readonly IVitalSignRepository _vitalSigns;
    private readonly ILogger<EnvironmentHealthAnalyticsService> _logger;

    public EnvironmentHealthAnalyticsService(
        IEnvironmentReadingRepository environmentReadings,
        IVitalSignRepository vitalSigns,
        ILogger<EnvironmentHealthAnalyticsService> logger)
    {
        _environmentReadings = environmentReadings;
        _vitalSigns = vitalSigns;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnvironmentAnalyticsResult> ComputeLast24HoursAsync(
        Guid? patientId,
        CancellationToken cancellationToken = default)
    {
        var to = DateTime.UtcNow;
        var from = to.AddHours(-24);

        var envList = (await _environmentReadings.GetSinceAsync(from, 2000).ConfigureAwait(false))
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToList();

        var pmBuckets = AggregateMean(envList.Select(r => (r.Timestamp, Value: r.PM25)), from, to);

        List<(DateTime Timestamp, double Value)> hrSamples = [];
        if (patientId is not null)
        {
            try
            {
                hrSamples = (await _vitalSigns
                        .GetByPatientAsync(patientId.Value, VitalSignType.HeartRate, from, to)
                        .ConfigureAwait(false))
                    .OrderBy(v => v.Timestamp)
                    .Select(v => (v.Timestamp, v.Value))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to load heart rate vitals for analytics.");
            }
        }

        var hrBuckets = AggregateMean(hrSamples, from, to);

        var hrPath = BuildSvgPath(hrBuckets);
        var pmPath = BuildSvgPath(pmBuckets);

        var (r, footnote) = ComputeCorrelationAndFootnote(pmBuckets, hrBuckets, envList.Count, hrSamples.Count);

        return new EnvironmentAnalyticsResult
        {
            HeartRatePath = hrPath,
            Pm25Path = pmPath,
            CorrelationR = r,
            Footnote = footnote,
            HasPm25Series = pmBuckets.Any(x => !double.IsNaN(x)),
            HasHeartRateSeries = hrBuckets.Any(x => !double.IsNaN(x))
        };
    }

    private static double[] AggregateMean(IEnumerable<(DateTime Timestamp, double Value)> points, DateTime from, DateTime to)
    {
        var sums = new double[Buckets];
        var counts = new int[Buckets];
        foreach (var (ts, val) in points)
        {
            if (ts < from || ts > to) continue;
            var idx = (int)Math.Floor((ts - from).TotalHours);
            if (idx < 0 || idx >= Buckets) continue;
            sums[idx] += val;
            counts[idx]++;
        }

        var result = new double[Buckets];
        for (var i = 0; i < Buckets; i++)
            result[i] = counts[i] > 0 ? sums[i] / counts[i] : double.NaN;
        return result;
    }

    private static string BuildSvgPath(double[] bucketMeans)
    {
        var vals = bucketMeans.Where(v => !double.IsNaN(v)).ToList();
        if (vals.Count == 0)
            return string.Empty;

        var min = vals.Min();
        var max = vals.Max();
        if (Math.Abs(max - min) < 1e-6)
        {
            min -= 1;
            max += 1;
        }

        var parts = new List<string>();
        for (var i = 0; i < Buckets; i++)
        {
            if (double.IsNaN(bucketMeans[i])) continue;
            var x = Buckets <= 1 ? 50.0 : i / (double)(Buckets - 1) * 100.0;
            var y = 50.0 - (bucketMeans[i] - min) / (max - min) * 45.0;
            y = Math.Clamp(y, 2, 48);
            parts.Add(parts.Count == 0 ? $"M {x:F2},{y:F2}" : $"L {x:F2},{y:F2}");
        }

        return string.Join(" ", parts);
    }

    private static (double? R, string Footnote) ComputeCorrelationAndFootnote(
        double[] pm,
        double[] hr,
        int rawEnvCount,
        int rawHrCount)
    {
        var pairs = new List<(double X, double Y)>();
        for (var i = 0; i < Buckets; i++)
        {
            if (double.IsNaN(pm[i]) || double.IsNaN(hr[i])) continue;
            pairs.Add((pm[i], hr[i]));
        }

        if (pairs.Count < MinPairsForCorrelation)
        {
            var bits = new List<string>();
            if (rawEnvCount == 0)
                bits.Add("No saved air-quality samples in the last 24 hours yet.");
            else if (pairs.Count == 0)
                bits.Add("Air quality and heart rate do not overlap by hour in this window.");

            if (rawHrCount == 0)
                bits.Add("No heart rate samples in the local database for the last 24 hours.");

            if (bits.Count == 0)
                bits.Add("Not enough overlapping hourly data to estimate correlation (need at least 8 hours with both PM2.5 and HR).");

            bits.Add("Domain risk threshold: PM2.5 above 35 µg/m³ may trigger alerts.");
            return (null, string.Join(" ", bits));
        }

        var xs = pairs.Select(p => p.X).ToArray();
        var ys = pairs.Select(p => p.Y).ToArray();
        var mx = xs.Average();
        var my = ys.Average();
        double sxx = 0, syy = 0, sxy = 0;
        for (var i = 0; i < xs.Length; i++)
        {
            var dx = xs[i] - mx;
            var dy = ys[i] - my;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }

        var denom = Math.Sqrt(sxx * syy);
        var r = denom > 1e-12 ? sxy / denom : 0;
        r = Math.Clamp(r, -1, 1);

        var interp = Math.Abs(r) < 0.15
            ? "Little linear relationship between hourly PM2.5 and heart rate in this window."
            : r > 0
                ? "Higher PM2.5 tends to coincide with higher heart rate in this window."
                : "Higher PM2.5 tends to coincide with lower heart rate in this window (many factors affect HR).";

        return (Math.Round(r, 2), $"{interp} Pearson r ≈ {r:F2} over {pairs.Count} hourly buckets. Domain risk threshold: PM2.5 > 35 µg/m³.");
    }
}
