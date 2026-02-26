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
