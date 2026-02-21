using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Mappers;

public static class MedicationInteractionMapper
{
    public static MedicationInteractionDto ToDto(MedicationInteraction model) => new()
    {
        DrugARxCui = model.DrugARxCui,
        DrugBRxCui = model.DrugBRxCui,
        Severity = EnumMapper.ToApp(model.Severity),
        Description = model.Description
    };
}
