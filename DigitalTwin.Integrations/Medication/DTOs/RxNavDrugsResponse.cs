using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record RxNavDrugsResponse(
    [property: JsonPropertyName("drugGroup")] DrugGroup? DrugGroup);

internal sealed record DrugGroup(
    [property: JsonPropertyName("conceptGroup")] List<ConceptGroup>? ConceptGroup);

internal sealed record ConceptGroup(
    [property: JsonPropertyName("conceptProperties")] List<ConceptProperty>? ConceptProperties);

internal sealed record ConceptProperty(
    [property: JsonPropertyName("rxcui")] string? Rxcui,
    [property: JsonPropertyName("name")] string? Name);
