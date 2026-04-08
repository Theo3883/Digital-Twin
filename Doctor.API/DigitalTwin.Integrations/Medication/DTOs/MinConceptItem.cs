using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record MinConceptItem(
    [property: JsonPropertyName("rxcui")] string? Rxcui,
    [property: JsonPropertyName("name")] string? Name);
