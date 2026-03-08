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
/// </summary>
public class PatientContextService : IPatientContextService
{
    private static readonly TimeSpan VitalsLookback = TimeSpan.FromHours(24);

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
}
