using IOSHealthApp.Application.DTOs;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Application.Mappers;

public static class EnvironmentReadingMapper
{
    public static EnvironmentReadingDto ToDto(EnvironmentReading model)
    {
        return new EnvironmentReadingDto
        {
            PM25 = model.PM25,
            Temperature = model.Temperature,
            Humidity = model.Humidity,
            AirQuality = model.AirQuality,
            Timestamp = model.Timestamp
        };
    }
}
