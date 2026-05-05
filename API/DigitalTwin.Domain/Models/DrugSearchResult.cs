namespace DigitalTwin.Domain.Models;

/// <summary>
/// A drug name + RxCUI pair returned from a drug-search lookup.
/// Used to let patients find their medication by name rather than needing to know the RxCUI code.
/// </summary>
public record DrugSearchResult(string Name, string RxCui);
