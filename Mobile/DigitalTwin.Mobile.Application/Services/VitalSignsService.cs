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

    public VitalSignsService(
        IVitalSignRepository vitalSignRepository,
        IPatientRepository patientRepository,
        ILogger<VitalSignsService> logger)
    {
        _vitalSignRepository = vitalSignRepository;
        _patientRepository = patientRepository;
        _logger = logger;
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

            var vitalSign = new VitalSign
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                Type = input.Type,
                Value = input.Value,
                Unit = input.Unit,
                Source = input.Source,
                Timestamp = input.Timestamp ?? DateTime.UtcNow,
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

        var vitalSigns = inputs.Select(input => new VitalSign
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Type = input.Type,
            Value = input.Value,
            Unit = input.Unit,
            Source = input.Source,
            Timestamp = input.Timestamp ?? DateTime.UtcNow,
            IsSynced = false
        }).ToList();

        await _vitalSignRepository.SaveRangeAsync(vitalSigns);
        _logger.LogInformation("[VitalSignsService] Recorded {Count} vital signs", vitalSigns.Count);

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