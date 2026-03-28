using DigitalTwin.Domain.Models;

namespace DigitalTwin.Integrations.AI;

/// <summary>
/// Shared placeholder replacement for persisted medical profile + derived vitals on <see cref="PatientProfile"/>.
/// </summary>
internal static class GeminiPatientContextFormatting
{
    public static string ReplaceMedicalProfilePlaceholders(string format, PatientProfile? profile)
    {
        if (profile is null)
        {
            return format
                .Replace("{bloodType}", "N/A")
                .Replace("{allergies}", "N/A")
                .Replace("{medicalHistoryNotes}", "N/A")
                .Replace("{weight}", "N/A")
                .Replace("{height}", "N/A")
                .Replace("{bloodPressure}", "N/A")
                .Replace("{cholesterol}", "N/A")
                .Replace("{bmi}", "N/A")
                .Replace("{restingHr}", "N/A");
        }

        return format
            .Replace("{bloodType}", string.IsNullOrWhiteSpace(profile.BloodType) ? "N/A" : profile.BloodType)
            .Replace("{allergies}", string.IsNullOrWhiteSpace(profile.Allergies) ? "N/A" : profile.Allergies)
            .Replace("{medicalHistoryNotes}",
                string.IsNullOrWhiteSpace(profile.MedicalHistoryNotes) ? "N/A" : profile.MedicalHistoryNotes)
            .Replace("{weight}", profile.Weight?.ToString("0.#") ?? "N/A")
            .Replace("{height}", profile.Height?.ToString("0.#") ?? "N/A")
            .Replace("{bloodPressure}", FormatBloodPressure(profile))
            .Replace("{cholesterol}", profile.Cholesterol?.ToString("0.0") ?? "N/A")
            .Replace("{bmi}", profile.Bmi?.ToString("0.0") ?? "N/A")
            .Replace("{restingHr}", profile.RestingHeartRateBpm?.ToString() ?? "N/A");
    }

    private static string FormatBloodPressure(PatientProfile profile)
    {
        if (profile.BloodPressureSystolic is not null && profile.BloodPressureDiastolic is not null)
            return $"{profile.BloodPressureSystolic}/{profile.BloodPressureDiastolic}";
        return "N/A";
    }
}
