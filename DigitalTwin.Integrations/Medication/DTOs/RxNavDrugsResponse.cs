using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

// ── drugs.json (drug name search / autocomplete) ─────────────────────────────
// GET /REST/drugs.json?name={query}
internal sealed record RxNavDrugsResponse(
    [property: JsonPropertyName("drugGroup")] DrugGroup? DrugGroup);

internal sealed record DrugGroup(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("conceptGroup")] List<ConceptGroup>? ConceptGroup);

internal sealed record ConceptGroup(
    [property: JsonPropertyName("tty")] string? Tty,
    [property: JsonPropertyName("conceptProperties")] List<ConceptProperty>? ConceptProperties);

internal sealed record ConceptProperty(
    [property: JsonPropertyName("rxcui")] string? Rxcui,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("synonym")] string? Synonym,
    [property: JsonPropertyName("tty")] string? Tty,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("suppress")] string? Suppress,
    [property: JsonPropertyName("umlscui")] string? UmlsCui);

// ── related.json?tty=IN (ingredient normalisation) ───────────────────────────
// GET /REST/rxcui/{id}/related.json?tty=IN
internal sealed record RxNavRelatedResponse(
    [property: JsonPropertyName("relatedGroup")] RelatedGroupDto? RelatedGroup);

internal sealed record RelatedGroupDto(
    [property: JsonPropertyName("rxcui")] string? Rxcui,
    [property: JsonPropertyName("conceptGroup")] List<ConceptGroup>? ConceptGroup);
