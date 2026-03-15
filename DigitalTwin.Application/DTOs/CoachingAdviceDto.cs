namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a coaching response generated for the user.
/// </summary>
public class CoachingAdviceDto
{
    /// <summary>
    /// Gets or sets the coaching advice text.
    /// </summary>
    public string Advice { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the advice was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
