using System.Net.Http.Json;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Integrations.Medication.DTOs;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Integrations.Medication;

/// <summary>
/// Resolves a medication name in any form — brand name, generic, international name,
/// or approximate spelling — to an ingredient-level RxCUI using the RxNav API.
///
/// Resolution pipeline:
///   1. <c>approximateTerm.json</c> — fuzzy lexical search returns ranked candidate CUIs.
///      Handles brand names (Nurofen → ibuprofen), typos, and alternate spellings.
///   2. For each candidate: <c>rxcui/{id}/related.json?tty=IN</c> normalises product/brand
///      CUIs to the underlying ingredient concept.
///   3. Returns the first valid ingredient CUI, or <c>null</c> if nothing resolves.
/// </summary>
public sealed class RxNavRxCuiResolver : IRxCuiLookupProvider
{
    private const string BaseUrl = "https://rxnav.nlm.nih.gov/REST";
    private const int MaxCandidates = 5;

    private readonly HttpClient _http;
    private readonly ILogger<RxNavRxCuiResolver> _logger;

    public RxNavRxCuiResolver(HttpClient http, ILogger<RxNavRxCuiResolver> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> LookupRxCuiAsync(string medicationName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(medicationName))
            return null;

        var candidates = await GetApproximateCandidatesAsync(medicationName.Trim(), ct);
        if (candidates.Count == 0)
            return null;

        // Normalise each candidate to ingredient level and return the first valid result.
        foreach (var rxCui in candidates)
        {
            var ingredientCui = await ResolveToIngredientAsync(rxCui, ct);
            if (string.IsNullOrWhiteSpace(ingredientCui))
                continue;

            // Guard against stale/retired RxCUIs that resolve but have no properties.
            var ingredientName = await ResolveNameAsync(ingredientCui, ct);
            if (!string.IsNullOrWhiteSpace(ingredientName))
            {
                _logger.LogInformation(
                    "[RxCUI] Resolved '{Medication}' -> candidate {CandidateRxCui} -> ingredient {IngredientRxCui} ({IngredientName})",
                    medicationName, rxCui, ingredientCui, ingredientName);
                return ingredientCui;
            }

            _logger.LogWarning(
                "[RxCUI] Skipping unresolved candidate for '{Medication}': candidate={CandidateRxCui}, ingredient={IngredientRxCui} has no properties/name.",
                medicationName, rxCui, ingredientCui);
        }

        _logger.LogWarning("[RxCUI] No valid RxCUI could be resolved for '{Medication}'.", medicationName);
        return null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<string>> GetApproximateCandidatesAsync(
        string term, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}/approximateTerm.json"
                    + $"?term={Uri.EscapeDataString(term)}"
                    + $"&maxEntries={MaxCandidates}";

            var response = await _http.GetFromJsonAsync<RxNavApproximateTermResponse>(url, ct);

            return response?.ApproximateGroup?.Candidate
                ?.Where(c => !string.IsNullOrWhiteSpace(c.Rxcui))
                .Select(c => c.Rxcui!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<string?> ResolveNameAsync(string rxCui, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}/rxcui/{rxCui}/properties.json";
            var response = await _http.GetFromJsonAsync<RxNavPropertiesResponse>(url, ct);
            return response?.Properties?.Name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a product/brand/salt CUI to its ingredient via
    /// <c>rxcui/{id}/related.json?tty=IN</c>.
    /// Returns the original CUI if already at ingredient level or on failure.
    /// </summary>
    private async Task<string?> ResolveToIngredientAsync(string rxCui, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}/rxcui/{rxCui}/related.json?tty=IN";
            var resp = await _http.GetFromJsonAsync<RxNavRelatedResponse>(url, ct);

            var ingredient = resp?.RelatedGroup?.ConceptGroup
                ?.SelectMany(g => g.ConceptProperties ?? [])
                .Select(p => p.Rxcui)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

            // If no ingredient found, the rxCui itself may already be at ingredient level.
            return ingredient ?? rxCui;
        }
        catch
        {
            return rxCui;
        }
    }
}
