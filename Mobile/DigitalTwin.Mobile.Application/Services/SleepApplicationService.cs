using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class SleepApplicationService
{
    private readonly ISleepSessionRepository _sleepRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly ILogger<SleepApplicationService> _logger;

    public SleepApplicationService(
        ISleepSessionRepository sleepRepo,
        IPatientRepository patientRepo,
        ILogger<SleepApplicationService> logger)
    {
        _sleepRepo = sleepRepo;
        _patientRepo = patientRepo;
        _logger = logger;
    }

    public async Task<bool> RecordSleepSessionAsync(SleepSessionInput input)
    {
        try
        {
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient == null)
            {
                _logger.LogWarning("[SleepApp] No patient found");
                return false;
            }

            // Deduplicate by patient + start time
            if (await _sleepRepo.ExistsAsync(patient.Id, input.StartTime))
            {
                return true;
            }

            var session = new SleepSession
            {
                PatientId = patient.Id,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                DurationMinutes = input.DurationMinutes,
                QualityScore = input.QualityScore
            };

            await _sleepRepo.SaveAsync(session);
            _logger.LogInformation("[SleepApp] Recorded sleep session: {Duration}min, quality={Quality}",
                session.DurationMinutes, session.QualityScore);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SleepApp] Failed to record sleep session");
            return false;
        }
    }

    public async Task<int> RecordSleepSessionsAsync(IEnumerable<SleepSessionInput> inputs)
    {
        var patient = await _patientRepo.GetCurrentPatientAsync();
        if (patient == null) return 0;

        var sessions = new List<SleepSession>();
        foreach (var input in inputs)
        {
            if (!await _sleepRepo.ExistsAsync(patient.Id, input.StartTime))
            {
                sessions.Add(new SleepSession
                {
                    PatientId = patient.Id,
                    StartTime = input.StartTime,
                    EndTime = input.EndTime,
                    DurationMinutes = input.DurationMinutes,
                    QualityScore = input.QualityScore
                });
            }
        }

        if (sessions.Count > 0)
            await _sleepRepo.SaveRangeAsync(sessions);

        return sessions.Count;
    }

    public async Task<IEnumerable<SleepSessionDto>> GetSleepSessionsAsync(DateTime? from = null, DateTime? to = null)
    {
        _logger.LogInformation("[SleepDebug][SleepApp] GetSleepSessionsAsync request. from={From} to={To}",
            from?.ToString("O") ?? "nil",
            to?.ToString("O") ?? "nil");

        var patient = await _patientRepo.GetCurrentPatientAsync();
        if (patient == null)
        {
            _logger.LogWarning("[SleepDebug][SleepApp] No current patient found while fetching sleep sessions");
            return [];
        }

        
        var sessions = await _sleepRepo.GetByPatientIdAsync(patient.Id, from, to);
        var mapped = sessions.Select(s => new SleepSessionDto
        {
            Id = s.Id,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationMinutes = s.DurationMinutes,
            QualityScore = s.QualityScore,
            IsSynced = s.IsSynced
        }).OrderByDescending(s => s.StartTime);

        var mappedArray = mapped.ToArray();
        if (mappedArray.Length > 0)
        {
            var latest = mappedArray[0];
        }
        else
        {
            _logger.LogInformation("[SleepDebug][SleepApp] Returning sleep sessions. count=0");
        }

        return mappedArray;
    }
}
