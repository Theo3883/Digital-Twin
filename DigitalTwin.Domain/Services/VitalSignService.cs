using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

public class VitalSignService
{
    /// <summary>
    /// Determines the trend direction by comparing the two most recent values.
    /// Returns +1 (rising), -1 (falling), or 0 (stable).
    /// </summary>
    public int ComputeTrend(double currentValue, double previousValue)
    {
        var delta = currentValue - previousValue;
        const double threshold = 0.5;

        if (delta > threshold) return 1;
        if (delta < -threshold) return -1;
        return 0;
    }

    /// <summary>
    /// Validates whether a vital sign value falls within a clinically plausible range.
    /// </summary>
    public bool IsInValidRange(VitalSign vitalSign)
    {
        return vitalSign.Type switch
        {
            VitalSignType.HeartRate => vitalSign.Value is >= 20 and <= 300,
            VitalSignType.SpO2 => vitalSign.Value is >= 0 and <= 100,
            VitalSignType.Steps => vitalSign.Value >= 0,
            VitalSignType.Calories => vitalSign.Value >= 0,
            _ => false
        };
    }

    public string GetUnitForType(VitalSignType type)
    {
        return type switch
        {
            VitalSignType.HeartRate => "bpm",
            VitalSignType.SpO2 => "%",
            VitalSignType.Steps => "steps",
            VitalSignType.Calories => "kcal",
            _ => ""
        };
    }
}
