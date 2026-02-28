using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Entities;

namespace DigitalTwin.Infrastructure.Mappers;

internal static class EnvironmentReadingEntityMapper
{
    internal static EnvironmentReading ToDomain(EnvironmentReadingEntity e) => new()
    {
        Latitude    = (double)e.Latitude,
        Longitude   = (double)e.Longitude,
        PM25        = (double)e.PM25,
        PM10        = (double)e.PM10,
        O3          = (double)e.O3,
        NO2         = (double)e.NO2,
        Temperature = (double)e.Temperature,
        Humidity    = (double)e.Humidity,
        AirQuality  = (AirQualityLevel)e.AirQualityLevel,
        AqiIndex    = e.AqiIndex,
        Timestamp   = e.Timestamp
    };

    internal static EnvironmentReadingEntity ToEntity(EnvironmentReading m) => new()
    {
        Latitude         = (decimal)m.Latitude,
        Longitude        = (decimal)m.Longitude,
        PM25             = (decimal)m.PM25,
        PM10             = (decimal)m.PM10,
        O3               = (decimal)m.O3,
        NO2              = (decimal)m.NO2,
        Temperature      = (decimal)m.Temperature,
        Humidity         = (decimal)m.Humidity,
        AirQualityLevel  = (int)m.AirQuality,
        AqiIndex         = m.AqiIndex,
        Timestamp        = m.Timestamp
    };
}
