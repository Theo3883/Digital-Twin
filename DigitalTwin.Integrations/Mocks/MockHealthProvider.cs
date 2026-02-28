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
        return Observable.Interval(TimeSpan.FromSeconds(2))
            .SelectMany(_ => GenerateVitals().ToList());
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

    private List<VitalSign> GenerateVitals()
    {
        var now = DateTime.UtcNow;
        var elapsed = now.TimeOfDay.TotalSeconds;

        var heartRate = new VitalSign
        {
            Type = VitalSignType.HeartRate,
            Value = Math.Round(80 + 20 * Math.Sin(elapsed / 30.0) + _random.Next(-3, 4), 1),
            Unit = "bpm",
            Source = "Mock",
            Timestamp = now
        };

        var spo2 = new VitalSign
        {
            Type = VitalSignType.SpO2,
            Value = Math.Round(97 + 2 * Math.Sin(elapsed / 60.0) + _random.NextDouble() * 0.5, 1),
            Unit = "%",
            Source = "Mock",
            Timestamp = now
        };

        var hourOfDay = now.Hour + now.Minute / 60.0;
        var steps = new VitalSign
        {
            Type = VitalSignType.Steps,
            Value = Math.Round(hourOfDay * 450 + _random.Next(0, 100)),
            Unit = "steps",
            Source = "Mock",
            Timestamp = now
        };

        var calories = new VitalSign
        {
            Type = VitalSignType.Calories,
            Value = Math.Round(steps.Value * 0.04 + 1200 + _random.Next(0, 50)),
            Unit = "kcal",
            Source = "Mock",
            Timestamp = now
        };

        var activeEnergy = new VitalSign
        {
            Type = VitalSignType.ActiveEnergy,
            Value = Math.Round(hourOfDay * 35 + _random.Next(0, 20)),
            Unit = "kcal",
            Source = "Mock",
            Timestamp = now
        };

        var exerciseMinutes = new VitalSign
        {
            Type = VitalSignType.ExerciseMinutes,
            Value = Math.Round(Math.Min(hourOfDay * 2.5 + _random.Next(0, 5), 120)),
            Unit = "min",
            Source = "Mock",
            Timestamp = now
        };

        var standHours = new VitalSign
        {
            Type = VitalSignType.StandHours,
            Value = Math.Min((int)(hourOfDay * 0.6) + _random.Next(0, 2), 24),
            Unit = "hrs",
            Source = "Mock",
            Timestamp = now
        };

        var vitals = new List<VitalSign>
        {
            heartRate, spo2, steps, calories,
            activeEnergy, exerciseMinutes, standHours
        };

        lock (_sync)
        {
            _buffer.AddRange(vitals);
            if (_buffer.Count > 700)
                _buffer.RemoveRange(0, _buffer.Count - 700);
        }

        return vitals;
    }
}
