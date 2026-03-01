namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record InteractionPair(string? Severity, string? Description, List<InteractionConcept>? InteractionConcept);
