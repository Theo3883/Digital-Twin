using System.Reactive.Linq;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Integrations.Mocks;

public class MockHealthProvider : IHealthDataProvider
{
    private readonly Random _random = new();
    private readonly List<VitalSign> _buffer = [];
    private readonly object _sync = new();
    private int _dailySteps = 0;
    private DateTime _lastStepsDate = DateTime.MinValue;

    public IObservable<VitalSign> GetLiveVitals()
    {
        return Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(15))
            .SelectMany(_ => GenerateLiveVitals());
    }

    public Task<IEnumerable<VitalSign>> GetLatestSamplesAsync(VitalSignType type, int count = 10)
    {
        lock (_sync)
        {
            var samples = _buffer
                .Where(v => v.Type == type)
                .OrderByDescending(v => v.Timestamp)
                .Take(count)
                .ToList();
            return Task.FromResult<IEnumerable<VitalSign>>(samples);
        }
    }

    public Task<IEnumerable<VitalSign>> GetSamplesAsync(VitalSignType type, DateTime fromUtc, DateTime toUtc, int maxSamples = 8000)
    {
        lock (_sync)
        {
            var samples = _buffer
                .Where(v => v.Type == type && v.Timestamp >= fromUtc && v.Timestamp <= toUtc)
                .OrderBy(v => v.Timestamp)
                .Take(maxSamples)
                .ToList();
            return Task.FromResult<IEnumerable<VitalSign>>(samples);
        }
    }

    public Task<IEnumerable<SleepSession>> GetSleepSessionsAsync(DateTime from, DateTime to)
    {
        // Generate a few mock sleep sessions within the requested range
        var sessions = new List<SleepSession>();
        var current = from.Date.AddHours(23); // Start at 11 PM

        while (current < to)
        {
            var duration = 360 + _random.Next(0, 120); // 6-8 hours
            var end = current.AddMinutes(duration);
            if (end > to) break;

            sessions.Add(new SleepSession
            {
                StartTime = current,
                EndTime = end,
                DurationMinutes = duration,
                QualityScore = Math.Round(60 + _random.NextDouble() * 35, 1)
            });

            current = current.AddDays(1);
        }

        return Task.FromResult<IEnumerable<SleepSession>>(sessions);
    }

    public Task<bool> RequestPermissionsAsync() => Task.FromResult(true);

    private IEnumerable<VitalSign> GenerateLiveVitals()
    {
        var now = DateTime.UtcNow;
        var vitals = new List<VitalSign>();

        // Heart rate — every tick
        var hr = new VitalSign
        {
            Type      = VitalSignType.HeartRate,
            Value     = Math.Round(80 + 20 * Math.Sin(now.TimeOfDay.TotalSeconds / 30.0) + _random.Next(-3, 4), 1),
            Unit      = "bpm",
            Source    = "Mock",
            Timestamp = now
        };
        vitals.Add(hr);

        // Blood oxygen — every tick alongside HR
        var spo2 = new VitalSign
        {
            Type      = VitalSignType.SpO2,
            Value     = Math.Round(96.0 + _random.NextDouble() * 3.0, 1), // 96.0–99.0 %
            Unit      = "%",
            Source    = "Mock",
            Timestamp = now
        };
        vitals.Add(spo2);

        // Steps — once per calendar day
        var today = now.Date;
        if (today != _lastStepsDate)
        {
            _dailySteps   = 4000 + _random.Next(0, 8001); // 4 000–12 000 steps
            _lastStepsDate = today;
        }
        var steps = new VitalSign
        {
            Type      = VitalSignType.Steps,
            Value     = _dailySteps,
            Unit      = "steps",
            Source    = "Mock",
            Timestamp = now
        };
        vitals.Add(steps);

        lock (_sync)
        {
            _buffer.AddRange(vitals);
            if (_buffer.Count > 700)
                _buffer.RemoveRange(0, _buffer.Count - 700);
        }

        return vitals;
    }
}
