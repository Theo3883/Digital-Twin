namespace DigitalTwin.Domain.Models;

/// <summary>
/// Composite read model aggregating a patient's health data for consumption by
/// the Doctor Portal domain service. Immutable value object.
/// </summary>
public sealed record PatientData(
    Patient Patient,
    User? User,
    IReadOnlyList<VitalSign> Vitals,
    IReadOnlyList<SleepSession> SleepSessions,
    IReadOnlyList<Medication> Medications);
