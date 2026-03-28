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

    public IObservable<VitalSign> GetLiveVitals()
    {
        return Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(15))
            .Select(_ => GenerateHeartRate());
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

    private VitalSign GenerateHeartRate()
    {
        var now = DateTime.UtcNow;
        var vital = new VitalSign
        {
            Type      = VitalSignType.HeartRate,
            Value     = Math.Round(80 + 20 * Math.Sin(now.TimeOfDay.TotalSeconds / 30.0) + _random.Next(-3, 4), 1),
            Unit      = "bpm",
            Source    = "Mock",
            Timestamp = now
        };

        lock (_sync)
        {
            _buffer.Add(vital);
            if (_buffer.Count > 700)
                _buffer.RemoveRange(0, _buffer.Count - 700);
        }

        return vital;
    }
}
