using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IVitalSignService
{
    int ComputeTrend(double currentValue, double previousValue);
    bool IsInValidRange(VitalSign vitalSign);
    string GetUnitForType(VitalSignType type);

    /// <summary>
    /// Attempts to parse a string to a <see cref="VitalSignType"/>.
    /// Returns false and sets <paramref name="type"/> to default when the value is null/empty or unrecognised.
    /// </summary>
    bool TryParseType(string? value, out VitalSignType type);
}
