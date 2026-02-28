#if IOS || MACCATALYST
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using Foundation;
using HealthKit;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Integrations.HealthKit;

/// <summary>
/// iOS HealthKit implementation of <see cref="IHealthDataProvider"/>.
/// Reads HR, SpO2, Steps, Calories, ActiveEnergy, ExerciseMinutes, StandHours, and Sleep data.
/// </summary>
public class HealthKitProvider : IHealthDataProvider
{
    private readonly HKHealthStore _store = new();
    private readonly ILogger<HealthKitProvider> _logger;

    private static readonly Dictionary<VitalSignType, HKQuantityTypeIdentifier> VitalTypeMap = new()
    {
        [VitalSignType.HeartRate] = HKQuantityTypeIdentifier.HeartRate,
        [VitalSignType.SpO2] = HKQuantityTypeIdentifier.OxygenSaturation,
        [VitalSignType.Steps] = HKQuantityTypeIdentifier.StepCount,
        [VitalSignType.Calories] = HKQuantityTypeIdentifier.BasalEnergyBurned,
        [VitalSignType.ActiveEnergy] = HKQuantityTypeIdentifier.ActiveEnergyBurned,
        [VitalSignType.ExerciseMinutes] = HKQuantityTypeIdentifier.AppleExerciseTime,
        // StandHours is HKCategoryTypeIdentifier – handled separately via QueryStandHoursAsync.
    };

    private static readonly Dictionary<VitalSignType, string> UnitMap = new()
    {
        [VitalSignType.HeartRate] = "bpm",
        [VitalSignType.SpO2] = "%",
        [VitalSignType.Steps] = "steps",
        [VitalSignType.Calories] = "kcal",
        [VitalSignType.ActiveEnergy] = "kcal",
        [VitalSignType.ExerciseMinutes] = "min",
        [VitalSignType.StandHours] = "hrs",
    };

    public HealthKitProvider(ILogger<HealthKitProvider> logger)
    {
        _logger = logger;
    }

    public async Task<bool> RequestPermissionsAsync()
    {
        if (!HKHealthStore.IsHealthDataAvailable)
        {
            _logger.LogWarning("[HealthKit] Health data is not available on this device.");
            return false;
        }

        var readTypes = new NSSet(
            HKQuantityType.Create(HKQuantityTypeIdentifier.HeartRate)!,
            HKQuantityType.Create(HKQuantityTypeIdentifier.OxygenSaturation)!,
            HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount)!,
            HKQuantityType.Create(HKQuantityTypeIdentifier.BasalEnergyBurned)!,
            HKQuantityType.Create(HKQuantityTypeIdentifier.ActiveEnergyBurned)!,
            HKQuantityType.Create(HKQuantityTypeIdentifier.AppleExerciseTime)!,
            HKCategoryType.Create(HKCategoryTypeIdentifier.AppleStandHour)!,
            HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!
        );

        var tcs = new TaskCompletionSource<bool>();

        _store.RequestAuthorizationToShare(
            typesToShare: new NSSet(),
            typesToRead: readTypes,
            (success, error) =>
            {
                if (error is not null)
                    _logger.LogError("[HealthKit] Permission request error: {Error}", error.LocalizedDescription);
                tcs.SetResult(success);
            });

        return await tcs.Task;
    }

    public IObservable<VitalSign> GetLiveVitals()
    {
        var subject = new Subject<VitalSign>();

        // Poll every 2 seconds to match the mock cadence
        var subscription = Observable.Interval(TimeSpan.FromSeconds(2))
            .Subscribe(async _ =>
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var since = now.AddSeconds(-5);

                    foreach (var (type, hkId) in VitalTypeMap)
                    {
                        var samples = await QueryLatestQuantityAsync(hkId, since, now, 1);
                        foreach (var sample in samples)
                        {
                            subject.OnNext(new VitalSign
                            {
                                Type = type,
                                Value = sample.value,
                                Unit = UnitMap[type],
                                Source = "HealthKit",
                                Timestamp = sample.timestamp
                            });
                        }
                    }

                    // StandHours is a category type — query separately
                    var standHours = await QueryStandHoursAsync(since, now);
                    if (standHours is not null)
                        subject.OnNext(standHours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HealthKit] Error polling live vitals.");
                }
            });

        return Observable.Create<VitalSign>(observer =>
        {
            var sub = subject.Subscribe(observer);
            return () =>
            {
                sub.Dispose();
                subscription.Dispose();
                subject.Dispose();
            };
        });
    }

    public async Task<IEnumerable<VitalSign>> GetLatestSamplesAsync(VitalSignType type, int count = 10)
    {
        var now = DateTime.UtcNow;
        var from = now.AddDays(-7);

        // StandHours is a category type — handle separately
        if (type == VitalSignType.StandHours)
        {
            var stand = await QueryStandHoursAsync(from, now);
            return stand is not null ? [stand] : [];
        }

        if (!VitalTypeMap.TryGetValue(type, out var hkId))
            return [];

        var results = await QueryLatestQuantityAsync(hkId, from, now, count);

        return results.Select(r => new VitalSign
        {
            Type = type,
            Value = r.value,
            Unit = UnitMap[type],
            Source = "HealthKit",
            Timestamp = r.timestamp
        });
    }

    public async Task<IEnumerable<SleepSession>> GetSleepSessionsAsync(DateTime from, DateTime to)
    {
        var sleepType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis);
        if (sleepType is null) return [];

        var predicate = HKQuery.GetPredicateForSamples(
            (NSDate)from, (NSDate)to, HKQueryOptions.None);

        var tcs = new TaskCompletionSource<IEnumerable<SleepSession>>();

        var query = new HKSampleQuery(
            sleepType, predicate, 0, null,
            (_, results, error) =>
            {
                if (error is not null || results is null)
                {
                    _logger.LogWarning("[HealthKit] Sleep query error: {Error}",
                        error?.LocalizedDescription ?? "null results");
                    tcs.SetResult([]);
                    return;
                }

                var sessions = results
                    .OfType<HKCategorySample>()
                    .Where(s => s.Value == (nint)(long)HKCategoryValueSleepAnalysis.Asleep
                             || s.Value == (nint)(long)HKCategoryValueSleepAnalysis.InBed)
                    .GroupBy(s => ((DateTime)s.StartDate).Date)
                    .Select(g =>
                    {
                        var start = g.Min(s => (DateTime)s.StartDate);
                        var end = g.Max(s => (DateTime)s.EndDate);
                        var duration = (int)(end - start).TotalMinutes;

                        // Estimate quality from % of time actually asleep vs in bed
                        var asleepMinutes = g
                            .Where(s => s.Value == (nint)(long)HKCategoryValueSleepAnalysis.Asleep)
                            .Sum(s => ((DateTime)s.EndDate - (DateTime)s.StartDate).TotalMinutes);
                        var quality = duration > 0 ? Math.Round(asleepMinutes / duration * 100, 1) : 0;

                        return new SleepSession
                        {
                            StartTime = start,
                            EndTime = end,
                            DurationMinutes = duration,
                            QualityScore = quality
                        };
                    })
                    .ToList();

                tcs.SetResult(sessions);
            });

        _store.ExecuteQuery(query);
        return await tcs.Task;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Queries AppleStandHour (category type). Each sample.Value == 0 means "stood",
    /// so we count the number of stood-samples in the range as hours.
    /// </summary>
    private Task<VitalSign?> QueryStandHoursAsync(DateTime from, DateTime to)
    {
        var standType = HKCategoryType.Create(HKCategoryTypeIdentifier.AppleStandHour);
        if (standType is null) return Task.FromResult<VitalSign?>(null);

        var predicate = HKQuery.GetPredicateForSamples(
            (NSDate)from, (NSDate)to, HKQueryOptions.None);

        var tcs = new TaskCompletionSource<VitalSign?>();

        var query = new HKSampleQuery(
            standType, predicate, 0, null,
            (_, results, error) =>
            {
                if (error is not null || results is null)
                {
                    tcs.SetResult(null);
                    return;
                }

                // Value 0 = "Stood", count those as stand hours
                var stoodCount = results
                    .OfType<HKCategorySample>()
                    .Count(s => s.Value == 0);

                tcs.SetResult(new VitalSign
                {
                    Type = VitalSignType.StandHours,
                    Value = stoodCount,
                    Unit = "hrs",
                    Source = "HealthKit",
                    Timestamp = DateTime.UtcNow
                });
            });

        _store.ExecuteQuery(query);
        return tcs.Task;
    }

    private Task<List<(double value, DateTime timestamp)>> QueryLatestQuantityAsync(
        HKQuantityTypeIdentifier identifier, DateTime from, DateTime to, int limit)
    {
        var quantityType = HKQuantityType.Create(identifier);
        if (quantityType is null)
            return Task.FromResult(new List<(double, DateTime)>());

        var predicate = HKQuery.GetPredicateForSamples(
            (NSDate)from, (NSDate)to, HKQueryOptions.None);

        var sortDescriptor = new NSSortDescriptor(
            HKSample.SortIdentifierEndDate, ascending: false);

        var tcs = new TaskCompletionSource<List<(double, DateTime)>>();

        var query = new HKSampleQuery(
            quantityType, predicate, (nuint)limit,
            [sortDescriptor],
            (_, results, error) =>
            {
                if (error is not null || results is null)
                {
                    tcs.SetResult([]);
                    return;
                }

                var unit = GetHKUnit(identifier);
                var values = results
                    .OfType<HKQuantitySample>()
                    .Select(s => (
                        value: Math.Round(s.Quantity.GetDoubleValue(unit), 1),
                        timestamp: (DateTime)s.EndDate))
                    .ToList();

                tcs.SetResult(values);
            });

        _store.ExecuteQuery(query);
        return tcs.Task;
    }

    private static HKUnit GetHKUnit(HKQuantityTypeIdentifier id) => id switch
    {
        HKQuantityTypeIdentifier.HeartRate =>
            HKUnit.Count.UnitDividedBy(HKUnit.Minute),
        HKQuantityTypeIdentifier.OxygenSaturation =>
            HKUnit.Percent,
        HKQuantityTypeIdentifier.StepCount =>
            HKUnit.Count,
        HKQuantityTypeIdentifier.BasalEnergyBurned or
        HKQuantityTypeIdentifier.ActiveEnergyBurned =>
            HKUnit.Kilocalorie,
        HKQuantityTypeIdentifier.AppleExerciseTime =>
            HKUnit.Minute,
        _ => HKUnit.Count
    };
}
#endif
