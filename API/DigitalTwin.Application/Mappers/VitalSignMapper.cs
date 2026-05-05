using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

/// <summary>
/// Converts vital-sign data between domain models and application DTOs.
/// </summary>
public static class VitalSignMapper
{
    /// <summary>
    /// Converts a domain vital sign to an application DTO.
    /// </summary>
    public static VitalSignDto ToDto(VitalSign model, int trend = 0)
    {
        return new VitalSignDto
        {
            Type = EnumMapper.ToApp(model.Type),
            Value = model.Value,
            Unit = model.Unit,
            Timestamp = model.Timestamp,
            Trend = trend
        };
    }

    /// <summary>
    /// Converts a vital-sign DTO to a domain vital-sign model.
    /// </summary>
    public static VitalSign ToModel(VitalSignDto dto)
    {
        return new VitalSign
        {
            Type = EnumMapper.ToDomain(dto.Type),
            Value = dto.Value,
            Unit = dto.Unit,
            Timestamp = dto.Timestamp
        };
    }
}
