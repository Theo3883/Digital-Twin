using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class CoachingApplicationService
{
    private readonly ICoachingProvider _coachingProvider;
    private readonly IPatientRepository _patientRepo;
    private readonly ILogger<CoachingApplicationService> _logger;

    private CoachingAdviceDto? _cachedAdvice;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    public CoachingApplicationService(
        ICoachingProvider coachingProvider,
        IPatientRepository patientRepo,
        ILogger<CoachingApplicationService> logger)
    {
        _coachingProvider = coachingProvider;
        _patientRepo = patientRepo;
        _logger = logger;
    }

    public async Task<CoachingAdviceDto> GetAdviceAsync()
    {
        if (_cachedAdvice != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedAdvice;

        try
        {
            var patient = await _patientRepo.GetCurrentPatientAsync();
            string context = "General health coaching";
            if (patient != null)
            {
                context = $"Patient: BloodType={patient.BloodType}, Weight={patient.Weight}kg, Height={patient.Height}cm, Allergies={patient.Allergies}";
            }

            var advice = await _coachingProvider.GetAdviceAsync(context);

            _cachedAdvice = new CoachingAdviceDto
            {
                Advice = advice,
                Timestamp = DateTime.UtcNow
            };
            _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);

            _logger.LogInformation("[Coaching] Generated advice ({Length} chars)", advice.Length);
            return _cachedAdvice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coaching] Failed to get advice");
            return new CoachingAdviceDto
            {
                Advice = "Stay hydrated, get regular exercise, and maintain a balanced diet.",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<CoachingAdviceDto> GetEnvironmentAdviceAsync(EnvironmentReadingDto? envReading)
    {
        try
        {
            if (envReading == null)
                return new CoachingAdviceDto { Advice = "No environment data available.", Timestamp = DateTime.UtcNow };

            var patient = await _patientRepo.GetCurrentPatientAsync();
            var context = $"Environment: AQI={envReading.AqiIndex} ({envReading.AirQuality}), PM2.5={envReading.PM25}, Temperature={envReading.Temperature}°C, Humidity={envReading.Humidity}%";
            if (patient != null)
                context += $", Patient: Allergies={patient.Allergies}";

            var advice = await _coachingProvider.GetAdviceAsync(context);
            return new CoachingAdviceDto { Advice = advice, Timestamp = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coaching] Failed to get environment advice");
            return new CoachingAdviceDto
            {
                Advice = "Monitor air quality and limit outdoor activity when AQI is high.",
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
