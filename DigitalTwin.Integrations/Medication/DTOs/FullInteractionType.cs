using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record FullInteractionType(
    [property: JsonPropertyName("interactionPair")] List<InteractionPair>? InteractionPair);
