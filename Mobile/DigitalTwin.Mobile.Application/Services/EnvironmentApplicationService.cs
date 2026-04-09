using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class EnvironmentApplicationService
{
    private readonly IEnvironmentDataProvider _envProvider;
    private readonly IEnvironmentReadingRepository _envRepo;
    private readonly EnvironmentAssessmentService _assessment;
    private readonly ILogger<EnvironmentApplicationService> _logger;

    public EnvironmentApplicationService(
        IEnvironmentDataProvider envProvider,
        IEnvironmentReadingRepository envRepo,
        EnvironmentAssessmentService assessment,
        ILogger<EnvironmentApplicationService> logger)
    {
        _envProvider = envProvider;
        _envRepo = envRepo;
        _assessment = assessment;
        _logger = logger;
    }

    public async Task<EnvironmentReadingDto?> GetCurrentEnvironmentAsync(double latitude, double longitude)
    {
        try
        {
            var reading = await _envProvider.GetCurrentAsync(latitude, longitude);
            var assessed = _assessment.AssessReading(reading);
            await _envRepo.SaveAsync(assessed);

            _logger.LogInformation("[EnvironmentApp] Fetched reading: AQI={Aqi}, Temp={Temp}°C",
                assessed.AqiIndex, assessed.Temperature);

            return MapToDto(assessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnvironmentApp] Failed to get environment data");
            return null;
        }
    }

    public async Task<EnvironmentReadingDto?> GetLatestCachedAsync()
    {
        var reading = await _envRepo.GetLatestAsync();
        return reading != null ? MapToDto(reading) : null;
    }

    private static EnvironmentReadingDto MapToDto(Domain.Models.EnvironmentReading r) => new()
    {
        Latitude = r.Latitude,
        Longitude = r.Longitude,
        LocationDisplayName = r.LocationDisplayName,
        PM25 = r.PM25,
        PM10 = r.PM10,
        O3 = r.O3,
        NO2 = r.NO2,
        Temperature = r.Temperature,
        Humidity = r.Humidity,
        AirQuality = r.AirQuality,
        AqiIndex = r.AqiIndex,
        Timestamp = r.Timestamp
    };
}
