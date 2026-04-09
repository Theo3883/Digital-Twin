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
            VitalSignType.BloodPressure => vitalSign.Value is >= 20 and <= 300,
            VitalSignType.Temperature => vitalSign.Value is >= 30 and <= 45,
            VitalSignType.OxygenSaturation => vitalSign.Value is >= 0 and <= 100,
            VitalSignType.RespiratoryRate => vitalSign.Value is >= 0 and <= 100,
            VitalSignType.BloodGlucose => vitalSign.Value is >= 0 and <= 1000,
            VitalSignType.Weight => vitalSign.Value is >= 0 and <= 500,
            VitalSignType.Height => vitalSign.Value is >= 0 and <= 300,
            VitalSignType.BMI => vitalSign.Value is >= 5 and <= 100,
            VitalSignType.StepCount => vitalSign.Value >= 0,
            VitalSignType.CaloriesBurned => vitalSign.Value >= 0,
            VitalSignType.SleepDuration => vitalSign.Value is >= 0 and <= 1440,
            _ => false
        };
    }

    public string GetUnitForType(VitalSignType type)
    {
        return type switch
        {
            VitalSignType.HeartRate => "bpm",
            VitalSignType.BloodPressure => "mmHg",
            VitalSignType.Temperature => "°C",
            VitalSignType.OxygenSaturation => "%",
            VitalSignType.RespiratoryRate => "bpm",
            VitalSignType.BloodGlucose => "mg/dL",
            VitalSignType.Weight => "kg",
            VitalSignType.Height => "cm",
            VitalSignType.BMI => "kg/m²",
            VitalSignType.StepCount => "steps",
            VitalSignType.CaloriesBurned => "kcal",
            VitalSignType.SleepDuration => "min",
            _ => ""
        };
    }
}
