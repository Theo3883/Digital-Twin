using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record InteractionPair(
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("interactionConcept")] List<InteractionConcept>? InteractionConcept);
