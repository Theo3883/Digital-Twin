using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

/// <summary>
/// Converts ECG frames between domain models and application DTOs.
/// </summary>
public static class EcgFrameMapper
{
    /// <summary>
    /// Converts a domain ECG frame to its DTO representation.
    /// </summary>
    public static EcgFrameDto ToDto(EcgFrame model) => new()
    {
        Samples = model.Samples,
        SpO2 = model.SpO2,
        HeartRate = model.HeartRate,
        Timestamp = model.Timestamp
    };

    /// <summary>
    /// Converts an ECG frame DTO to its domain model representation.
    /// </summary>
    public static EcgFrame ToModel(EcgFrameDto dto) => new()
    {
        Samples = dto.Samples,
        SpO2 = dto.SpO2,
        HeartRate = dto.HeartRate,
        Timestamp = dto.Timestamp
    };
}
