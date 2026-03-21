using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record FullInteractionTypeGroup(
    [property: JsonPropertyName("fullInteractionType")] List<FullInteractionType>? FullInteractionType);
