using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record InteractionConcept(
    [property: JsonPropertyName("minConceptItem")] MinConceptItem? MinConceptItem);
