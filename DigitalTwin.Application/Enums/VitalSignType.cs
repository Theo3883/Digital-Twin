namespace DigitalTwin.Application.Enums;

/// <summary>
/// Defines vital-sign types exposed by the application layer.
/// </summary>
public enum VitalSignType
{
    /// <summary>
    /// Represents heart rate samples.
    /// </summary>
    HeartRate = 0,

    /// <summary>
    /// Represents blood oxygen saturation samples.
    /// </summary>
    SpO2 = 1,

    /// <summary>
    /// Represents step count samples.
    /// </summary>
    Steps = 2,

    /// <summary>
    /// Represents calorie samples.
    /// </summary>
    Calories = 3,

    /// <summary>
    /// Represents active energy samples.
    /// </summary>
    ActiveEnergy = 4,

    /// <summary>
    /// Represents exercise minute samples.
    /// </summary>
    ExerciseMinutes = 5,

    /// <summary>
    /// Represents stand hour samples.
    /// </summary>
    StandHours = 6
}
