using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines the application contract for personalized coaching advice.
/// </summary>
public interface ICoachingApplicationService
{
    /// <summary>
    /// Generates coaching advice for the current user context.
    /// </summary>
    Task<CoachingAdviceDto> GetAdviceAsync(CancellationToken ct = default);

    /// <summary>
    /// Short environment-aware guidance from the current air-quality reading (Gemini when configured, otherwise heuristic).
    /// </summary>
    Task<CoachingAdviceDto> GetEnvironmentAdviceAsync(EnvironmentReadingDto environment, CancellationToken ct = default);

    /// <summary>
    /// Returns the last environment advice persisted to app preferences, or <c>null</c> if none exists yet.
    /// </summary>
    CoachingAdviceDto? GetLastEnvironmentAdvice();
}
