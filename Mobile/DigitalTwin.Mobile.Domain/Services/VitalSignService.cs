using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class VitalSignService
{
    public int ComputeTrend(double currentValue, double previousValue)
    {
        var delta = currentValue - previousValue;
        const double threshold = 0.5;

        if (delta > threshold) return 1;
        if (delta < -threshold) return -1;
        return 0;
    }

    public bool IsInValidRange(VitalSign vitalSign)
    {
        return vitalSign.Type switch
        {
            VitalSignType.HeartRate => vitalSign.Value is >= 20 and <= 300,
            VitalSignType.SpO2 => vitalSign.Value is >= 0 and <= 100,
            VitalSignType.Steps => vitalSign.Value >= 0,
            VitalSignType.Calories => vitalSign.Value >= 0,
            VitalSignType.ActiveEnergy => vitalSign.Value >= 0,
            VitalSignType.ExerciseMinutes => vitalSign.Value is >= 0 and <= 1440,
            VitalSignType.StandHours => vitalSign.Value is >= 0 and <= 24,
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
            VitalSignType.ActiveEnergy => "kcal",
            VitalSignType.ExerciseMinutes => "min",
            VitalSignType.StandHours => "hrs",
            _ => ""
        };
    }
}
