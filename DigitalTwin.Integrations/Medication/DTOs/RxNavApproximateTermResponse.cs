using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Medication.DTOs;

// ── approximateTerm.json (fuzzy brand/generic/typo → RxCUI) ──────────────────
// GET /REST/approximateTerm.json?term={name}&maxEntries=5
// Returns candidates ranked by lexical similarity score (best first).
internal sealed record RxNavApproximateTermResponse(
    [property: JsonPropertyName("approximateGroup")] ApproximateGroup? ApproximateGroup);

internal sealed record ApproximateGroup(
    [property: JsonPropertyName("inputTerm")] string? InputTerm,
    [property: JsonPropertyName("candidate")] List<ApproximateCandidate>? Candidate);

internal sealed record ApproximateCandidate(
    [property: JsonPropertyName("rxcui")] string? Rxcui,
    [property: JsonPropertyName("rxaui")] string? Rxaui,
    [property: JsonPropertyName("score")] string? Score,
    [property: JsonPropertyName("rank")] string? Rank);
