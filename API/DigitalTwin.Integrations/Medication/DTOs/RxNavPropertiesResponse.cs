using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

internal sealed record RxNavPropertiesResponse(
    [property: JsonPropertyName("properties")] RxNavProperties? Properties);

internal sealed record RxNavProperties(
    [property: JsonPropertyName("rxcui")] string? RxCui,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("tty")] string? Tty);
