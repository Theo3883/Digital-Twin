using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Services;

/// <summary>
/// Domain service that builds a <see cref="PatientProfile"/> enriched with
/// recent vital signs for use in AI prompts.
/// Business rules owned here:
/// - Which vitals to include and how far back to look.
/// - How to construct the AI context payload from raw domain models.
/// </summary>
public interface IPatientContextService
{
    /// <summary>
    /// Builds a <see cref="PatientProfile"/> for the currently authenticated user,
    /// loading recent vital signs from the local repository first.
    /// Returns <c>null</c> if no user is authenticated or no patient profile exists.
    /// </summary>
    Task<PatientProfile?> BuildContextAsync(CancellationToken ct = default);
}
