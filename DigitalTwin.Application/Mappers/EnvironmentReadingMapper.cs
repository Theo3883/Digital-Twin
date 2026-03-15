using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

/// <summary>
/// Converts assessed environment readings from domain models to DTOs.
/// </summary>
public static class EnvironmentReadingMapper
{
    /// <summary>
    /// Converts a domain environment reading to an application DTO.
    /// </summary>
    public static EnvironmentReadingDto ToDto(EnvironmentReading model)
    {
        return new EnvironmentReadingDto
        {
            PM25 = model.PM25,
            PM10 = model.PM10,
            O3 = model.O3,
            NO2 = model.NO2,
            Temperature = model.Temperature,
            Humidity = model.Humidity,
            AirQuality = EnumMapper.ToApp(model.AirQuality),
            AqiIndex = model.AqiIndex,
            Timestamp = model.Timestamp
        };
    }
}
