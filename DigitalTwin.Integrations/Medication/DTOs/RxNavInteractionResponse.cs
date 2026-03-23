using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

// ── list.json (multi-drug, batch endpoint) ───────────────────────────────────
// GET /REST/interaction/list.json?rxcuis={a+b+c}
internal sealed record RxNavListInteractionResponse(
    [property: JsonPropertyName("fullInteractionTypeGroup")] List<FullInteractionTypeGroup>? FullInteractionTypeGroup);

// ── interaction.json (single-drug endpoint, kept for reference) ──────────────
// GET /REST/interaction/interaction.json?rxcui={id}
internal sealed record RxNavSingleInteractionResponse(
    [property: JsonPropertyName("interactionTypeGroup")] List<SingleInteractionTypeGroup>? InteractionTypeGroup);

internal sealed record SingleInteractionTypeGroup(
    [property: JsonPropertyName("interactionType")] List<SingleInteractionType>? InteractionType);

internal sealed record SingleInteractionType(
    [property: JsonPropertyName("interactionPair")] List<InteractionPair>? InteractionPair);
