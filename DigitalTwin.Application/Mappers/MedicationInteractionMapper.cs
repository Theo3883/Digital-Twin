using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

/// <summary>
/// Converts medication interaction domain models to DTOs.
/// </summary>
public static class MedicationInteractionMapper
{
    /// <summary>
    /// Converts a domain medication interaction to an application DTO.
    /// </summary>
    public static MedicationInteractionDto ToDto(MedicationInteraction model) => new()
    {
        DrugARxCui = model.DrugARxCui,
        DrugBRxCui = model.DrugBRxCui,
        Severity = EnumMapper.ToApp(model.Severity),
        Description = model.Description
    };
}
