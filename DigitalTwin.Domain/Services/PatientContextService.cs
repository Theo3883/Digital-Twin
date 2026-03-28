using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Domain service that owns all business logic for building a
/// <see cref="PatientProfile"/> AI context payload.
/// Rules encapsulated here:
/// - Vitals lookback window is 24 hours.
/// - Local DB is always queried first (offline-capable).
/// - Only clinically relevant vital types are included.
/// - Trend is computed by comparing the two most recent readings per type.
/// - Persisted medical profile fields (weight, height, BP, cholesterol, etc.) are loaded from <see cref="Patient"/>.
/// - Resting HR estimate uses heart-rate samples from the last 7 days (lowest-decile average).
/// </summary>
public class PatientContextService : IPatientContextService
{
    private static readonly TimeSpan VitalsLookback = TimeSpan.FromHours(24);
    private static readonly TimeSpan RestingHrLookback = TimeSpan.FromDays(7);

    private static readonly VitalSignType[] RelevantVitalTypes =
    [
        VitalSignType.HeartRate,
        VitalSignType.SpO2,
        VitalSignType.Steps,
        VitalSignType.Calories,
        VitalSignType.ExerciseMinutes
    ];

    private readonly IAuthService _authService;
    private readonly IPatientService _patientService;
    private readonly IVitalSignRepository _vitalSignRepository;
    private readonly IVitalSignService _vitalSignService;

    public PatientContextService(
        IAuthService authService,
        IPatientService patientService,
        IVitalSignRepository vitalSignRepository,
        IVitalSignService vitalSignService)
    {
        _authService         = authService;
        _patientService      = patientService;
        _vitalSignRepository = vitalSignRepository;
        _vitalSignService    = vitalSignService;
    }

    public async Task<PatientProfile?> BuildContextAsync(CancellationToken ct = default)
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user is null)
        {
            return null;
        }

        var patientId = await _patientService.GetPatientIdForUserAsync(user.Id);
        if (patientId is null)
        {
            return null;
        }

        var profile = new PatientProfile
        {
            Id          = patientId.Value.ToString(),
            FullName    = BuildFullName(user),
            DateOfBirth = user.DateOfBirth ?? default
        };

        var patient = await _patientService.GetByUserIdAsync(user.Id);
        if (patient is not null)
        {
            profile.BloodType              = patient.BloodType;
            profile.Allergies              = patient.Allergies;
            profile.MedicalHistoryNotes    = patient.MedicalHistoryNotes;
            profile.Weight                 = patient.Weight;
            profile.Height                 = patient.Height;
            profile.BloodPressureSystolic  = patient.BloodPressureSystolic;
            profile.BloodPressureDiastolic = patient.BloodPressureDiastolic;
            profile.Cholesterol            = patient.Cholesterol;
            profile.Bmi                    = TryComputeBmi(patient.Weight, patient.Height);
        }

        profile.RestingHeartRateBpm = await TryComputeRestingHrBpmAsync(patientId.Value, ct);

        // Business rule: load from local DB first — always works offline.
        // Cloud sync happens independently; AI always uses whatever is locally available.
        profile.RecentVitals = await LoadAndFilterVitalsAsync(patientId.Value);
        profile.VitalTrends  = ComputeTrends(profile.RecentVitals);

        return profile;
    }

    // ── Private business rules ───────────────────────────────────────────────

    private static string BuildFullName(User user)
        => $"{user.FirstName} {user.LastName}".Trim();

    private async Task<List<VitalSign>> LoadAndFilterVitalsAsync(Guid patientId)
    {
        try
        {
            var from   = DateTime.UtcNow - VitalsLookback;
            var all    = await _vitalSignRepository.GetByPatientAsync(patientId, from: from);
            var result = new List<VitalSign>();

            foreach (var type in RelevantVitalTypes)
            {
                // Business rule: only include valid readings for relevant types.
                var readings = all
                    .Where(v => v.Type == type && _vitalSignService.IsInValidRange(v))
                    .OrderByDescending(v => v.Timestamp)
                    .Take(10)               // cap per type to keep prompt size bounded
                    .ToList();

                result.AddRange(readings);
            }

            return result;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Business rule: compute per-type trend (rising/stable/falling) by comparing
    /// the two most recent valid readings using <see cref="IVitalSignService"/>.
    /// </summary>
    private Dictionary<VitalSignType, int> ComputeTrends(List<VitalSign> vitals)
    {
        var trends = new Dictionary<VitalSignType, int>();

        foreach (var type in RelevantVitalTypes)
        {
            var readings = vitals
                .Where(v => v.Type == type)
                .OrderByDescending(v => v.Timestamp)
                .Take(2)
                .ToList();

            if (readings.Count == 2)
                trends[type] = _vitalSignService.ComputeTrend(readings[0].Value, readings[1].Value);
        }

        return trends;
    }

    private static decimal? TryComputeBmi(decimal? weightKg, decimal? heightCm)
    {
        if (!weightKg.HasValue || !heightCm.HasValue) return null;
        if (weightKg.Value <= 0 || heightCm.Value <= 0) return null;
        var heightM = heightCm.Value / 100m;
        if (heightM <= 0) return null;
        var bmi = weightKg.Value / (heightM * heightM);
        return bmi > 0 ? Math.Round(bmi, 2, MidpointRounding.AwayFromZero) : null;
    }

    private async Task<int?> TryComputeRestingHrBpmAsync(Guid patientId, CancellationToken ct)
    {
        try
        {
            var from = DateTime.UtcNow - RestingHrLookback;
            var samples = (await _vitalSignRepository.GetByPatientAsync(patientId, VitalSignType.HeartRate, from, null))
                .Where(v => _vitalSignService.IsInValidRange(v) && v.Value > 0)
                .Select(v => (decimal)v.Value)
                .OrderBy(v => v)
                .ToList();

            if (samples.Count < 10)
                return null;

            var take = Math.Max(1, (int)Math.Ceiling(samples.Count * 0.10m));
            var avg = samples.Take(take).Average();
            if (avg <= 0)
                return null;

            return (int)Math.Round(avg, MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
