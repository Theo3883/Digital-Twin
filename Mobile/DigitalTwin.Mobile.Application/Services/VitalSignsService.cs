using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Application service for vital signs management
/// </summary>
public class VitalSignsService
{
    private readonly IVitalSignRepository _vitalSignRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly ILogger<VitalSignsService> _logger;
    private readonly TimeZoneInfo _localTimeZone;

    public VitalSignsService(
        IVitalSignRepository vitalSignRepository,
        IPatientRepository patientRepository,
        ILogger<VitalSignsService> logger)
    {
        _vitalSignRepository = vitalSignRepository;
        _patientRepository = patientRepository;
        _logger = logger;
        _localTimeZone = TimeZoneInfo.Local;
    }

    private DateTime NormalizeTimestampForStorage(VitalSignType type, DateTime timestamp)
    {
        // Steps should be stored as exactly one row per day.
        if (type == VitalSignType.Steps)
        {
            // Convert to local date boundary, then store the day start as a DateTime.
            // This prevents "today" being split across UTC boundaries.
            var local = TimeZoneInfo.ConvertTime(timestamp, _localTimeZone);
            var localDayStart = local.Date;
            // Store as local time to match the key shape we already use elsewhere in the app.
            return localDayStart;
        }

        return timestamp;
    }

    /// <summary>
    /// Records a new vital sign reading
    /// </summary>
    public async Task<bool> RecordVitalSignAsync(VitalSignInput input)
    {
        try
        {
            var patient = await _patientRepository.GetCurrentPatientAsync();
            if (patient == null)
            {
                _logger.LogWarning("[VitalSignsService] No current patient found");
                return false;
            }

            var rawTimestamp = input.Timestamp ?? DateTime.UtcNow;
            var timestamp = NormalizeTimestampForStorage(input.Type, rawTimestamp);
            var existingId = await _vitalSignRepository.GetIdByKeyAsync(patient.Id, input.Type, timestamp, input.Source);

            var vitalSign = new VitalSign
            {
                // Idempotent upsert by (patient, type, timestamp, source)
                // If we previously stored a bad value (e.g. old steps aggregation), replace it.
                Id = existingId ?? Guid.NewGuid(),
                PatientId = patient.Id,
                Type = input.Type,
                Value = input.Value,
                Unit = input.Unit,
                Source = input.Source,
                Timestamp = timestamp,
                IsSynced = false
            };

            await _vitalSignRepository.SaveAsync(vitalSign);
            _logger.LogInformation("[VitalSignsService] Recorded {Type} vital sign: {Value} {Unit}", 
                input.Type, input.Value, input.Unit);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VitalSignsService] Failed to record vital sign");
            return false;
        }
    }

    /// <summary>
    /// Records multiple vital sign readings (e.g., from HealthKit)
    /// </summary>
    public async Task<int> RecordVitalSignsAsync(IEnumerable<VitalSignInput> inputs)
    {
        var patient = await _patientRepository.GetCurrentPatientAsync();
        if (patient == null)
        {
            _logger.LogWarning("[VitalSignsService] No current patient found");
            return 0;
        }

        var materialized = inputs.ToArray();
        if (materialized.Length == 0) return 0;

        var normalized = materialized
            .Select(i =>
            {
                var rawTs = i.Timestamp ?? DateTime.UtcNow;
                var storageTs = NormalizeTimestampForStorage(i.Type, rawTs);
                return new
                {
                    Input = i,
                    RawTimestamp = rawTs,
                    StorageTimestamp = storageTs
                };
            })
            .ToArray();

        // Steps: store one row per day, using the SUM of all incoming step samples for that day.
        // For other types: keep the inputs as-is.
        var aggregatedInputs = normalized
            .GroupBy(x => new { x.Input.Type, x.Input.Source, Ts = x.StorageTimestamp })
            .Select(g =>
            {
                var type = g.Key.Type;
                var value = type == VitalSignType.Steps ? g.Sum(x => x.Input.Value) : g.Last().Input.Value;
                var unit = g.Last().Input.Unit;
                return new VitalSignInput
                {
                    Type = type,
                    Source = g.Key.Source,
                    Timestamp = g.Key.Ts,
                    Unit = unit,
                    Value = value
                };
            })
            .ToArray();

        var minTs = aggregatedInputs.Min(x => x.Timestamp ?? DateTime.MinValue);
        var maxTs = aggregatedInputs.Max(x => x.Timestamp ?? DateTime.MaxValue);
        var existing = await _vitalSignRepository.GetByPatientIdAsync(patient.Id, minTs, maxTs);
        // There can be historical duplicates in the local DB (same type/timestamp/source).
        // Don't crash on ToDictionary; keep the most recent by CreatedAt (best-effort).
        var existingByKey = existing
            .GroupBy(v => $"{(int)v.Type}|{v.Timestamp:O}|{v.Source}", StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(v => v.CreatedAt).First(),
                StringComparer.Ordinal);

        var vitalSigns = aggregatedInputs
            .Select(input =>
            {
                var ts = input.Timestamp ?? DateTime.UtcNow;
                var key = $"{(int)input.Type}|{ts:O}|{input.Source}";
                var id = existingByKey.TryGetValue(key, out var existingVital)
                    ? existingVital.Id
                    : Guid.NewGuid();
                var value = input.Value;

                // For steps, we want the DB to represent the day-total. If we already have a record for that day,
                // keep the max to avoid regressing totals when partial batches arrive.
                if (input.Type == VitalSignType.Steps && existingVital != null)
                {
                    value = Math.Max(existingVital.Value, value);
                }

                return new VitalSign
                {
                    // Upsert by (type, timestamp, source) for this patient.
                    Id = id,
                    PatientId = patient.Id,
                    Type = input.Type,
                    Value = value,
                    Unit = input.Unit,
                    Source = input.Source,
                    Timestamp = ts,
                    IsSynced = false
                };
            })
            .ToList();

        await _vitalSignRepository.SaveRangeAsync(vitalSigns);
        _logger.LogInformation("[VitalSignsService] Recorded {Count} vital signs (incoming={IncomingCount})",
            vitalSigns.Count, materialized.Length);

        return vitalSigns.Count;
    }

    /// <summary>
    /// Gets vital signs for current patient
    /// </summary>
    public async Task<IEnumerable<VitalSignDto>> GetVitalSignsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var patient = await _patientRepository.GetCurrentPatientAsync();
        if (patient == null) return [];

        var vitals = await _vitalSignRepository.GetByPatientIdAsync(patient.Id, fromDate, toDate);
        
        return vitals.Select(v => new VitalSignDto
        {
            Id = v.Id,
            Type = v.Type,
            Value = v.Value,
            Unit = v.Unit,
            Source = v.Source,
            Timestamp = v.Timestamp,
            IsSynced = v.IsSynced
        }).OrderByDescending(v => v.Timestamp);
    }

    /// <summary>
    /// Gets vital signs by type
    /// </summary>
    public async Task<IEnumerable<VitalSignDto>> GetVitalSignsByTypeAsync(VitalSignType type, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var patient = await _patientRepository.GetCurrentPatientAsync();
        if (patient == null) return [];

        var vitals = await _vitalSignRepository.GetByTypeAsync(patient.Id, type, fromDate, toDate);
        
        return vitals.Select(v => new VitalSignDto
        {
            Id = v.Id,
            Type = v.Type,
            Value = v.Value,
            Unit = v.Unit,
            Source = v.Source,
            Timestamp = v.Timestamp,
            IsSynced = v.IsSynced
        }).OrderByDescending(v => v.Timestamp);
    }
}