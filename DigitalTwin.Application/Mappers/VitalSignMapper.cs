using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

public static class VitalSignMapper
{
    public static VitalSignDto ToDto(VitalSign model, int trend = 0)
    {
        return new VitalSignDto
        {
            Type = model.Type,
            Value = model.Value,
            Unit = model.Unit,
            Timestamp = model.Timestamp,
            Trend = trend
        };
    }

    public static VitalSign ToModel(VitalSignDto dto)
    {
        return new VitalSign
        {
            Type = dto.Type,
            Value = dto.Value,
            Unit = dto.Unit,
            Timestamp = dto.Timestamp
        };
    }
}
