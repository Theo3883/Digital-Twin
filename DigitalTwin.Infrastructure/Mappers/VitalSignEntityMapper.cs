using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Entities;

namespace DigitalTwin.Infrastructure.Mappers;

internal static class VitalSignEntityMapper
{
    internal static VitalSign ToDomain(VitalSignEntity e) => new()
    {
        PatientId = e.PatientId,
        Type      = (VitalSignType)e.Type,
        Value     = (double)e.Value,
        Unit      = e.Unit,
        Source    = e.Source ?? string.Empty,
        Timestamp = e.Timestamp
    };

    internal static VitalSignEntity ToEntity(VitalSign m) => new()
    {
        PatientId = m.PatientId,
        Type      = (int)m.Type,
        Value     = (decimal)m.Value,
        Unit      = m.Unit,
        Source    = m.Source,
        Timestamp = m.Timestamp
    };
}
