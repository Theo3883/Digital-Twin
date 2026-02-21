using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

public static class EnvironmentReadingMapper
{
    public static EnvironmentReadingDto ToDto(EnvironmentReading model)
    {
        return new EnvironmentReadingDto
        {
            PM25 = model.PM25,
            Temperature = model.Temperature,
            Humidity = model.Humidity,
            AirQuality = EnumMapper.ToApp(model.AirQuality),
            Timestamp = model.Timestamp
        };
    }
}
