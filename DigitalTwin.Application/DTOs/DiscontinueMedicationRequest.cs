namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a request to discontinue a medication.
/// </summary>
/// <param name="Reason">The reason for discontinuing the medication.</param>
public record DiscontinueMedicationRequest(string Reason);
