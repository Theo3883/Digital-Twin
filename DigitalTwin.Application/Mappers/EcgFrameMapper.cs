using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

public static class EcgFrameMapper
{
    public static EcgFrameDto ToDto(EcgFrame model) => new()
    {
        Samples = model.Samples,
        SpO2 = model.SpO2,
        HeartRate = model.HeartRate,
        Timestamp = model.Timestamp
    };

    public static EcgFrame ToModel(EcgFrameDto dto) => new()
    {
        Samples = dto.Samples,
        SpO2 = dto.SpO2,
        HeartRate = dto.HeartRate,
        Timestamp = dto.Timestamp
    };
}
