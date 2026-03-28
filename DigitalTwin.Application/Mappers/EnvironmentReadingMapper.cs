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
            Latitude = model.Latitude,
            Longitude = model.Longitude,
            LocationDisplayName = model.LocationDisplayName,
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

    public static EnvironmentReading ToDomain(EnvironmentReadingDto dto)
    {
        return new EnvironmentReading
        {
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            LocationDisplayName = dto.LocationDisplayName,
            PM25 = dto.PM25,
            PM10 = dto.PM10,
            O3 = dto.O3,
            NO2 = dto.NO2,
            Temperature = dto.Temperature,
            Humidity = dto.Humidity,
            AirQuality = EnumMapper.ToDomain(dto.AirQuality),
            AqiIndex = dto.AqiIndex,
            Timestamp = dto.Timestamp
        };
    }
}
