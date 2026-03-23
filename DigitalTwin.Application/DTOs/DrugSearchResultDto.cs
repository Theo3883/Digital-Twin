namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a medication search match with its RxCUI identifier.
/// </summary>
/// <param name="Name">The matched medication name.</param>
/// <param name="RxCui">The RxCUI identifier for the medication.</param>
public record DrugSearchResultDto(string Name, string RxCui);
