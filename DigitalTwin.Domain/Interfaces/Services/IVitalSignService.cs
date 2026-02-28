using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IVitalSignService
{
    int ComputeTrend(double currentValue, double previousValue);
    bool IsInValidRange(VitalSign vitalSign);
    string GetUnitForType(VitalSignType type);
}
