namespace DigitalTwin.Application.Enums;

/// <summary>
/// Defines medication interaction severity levels used by the application layer.
/// </summary>
public enum InteractionSeverity
{
    /// <summary>
    /// Indicates no known interaction.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates a low-severity interaction.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Indicates a medium-severity interaction.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Indicates a high-severity interaction.
    /// </summary>
    High = 3
}
