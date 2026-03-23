using System.Net.Http.Json;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using DigitalTwin.Integrations.Medication.DTOs;

namespace DigitalTwin.Integrations.Medication;

/// <summary>
/// Searches the RxNorm drug catalogue by name for use in autocomplete / drug pickers.
/// Each result carries both the display name and its ingredient-level RxCUI.
/// </summary>
public sealed class RxNavDrugSearchProvider : IDrugSearchProvider
{
    private const string BaseUrl = "https://rxnav.nlm.nih.gov/REST";

    private readonly HttpClient _http;

    public RxNavDrugSearchProvider(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<DrugSearchResult>> SearchByNameAsync(
        string query,
        int maxResults = 8,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        try
        {
            var url = $"{BaseUrl}/drugs.json?name={Uri.EscapeDataString(query)}";
            var response = await _http.GetFromJsonAsync<RxNavDrugsResponse>(url, ct);

            if (response?.DrugGroup?.ConceptGroup is null)
                return [];

            return response.DrugGroup.ConceptGroup
                .Where(g => g.ConceptProperties is not null)
                .SelectMany(g => g.ConceptProperties!)
                .Where(p => !string.IsNullOrWhiteSpace(p.Rxcui) && !string.IsNullOrWhiteSpace(p.Name))
                .Take(maxResults)
                .Select(p => new DrugSearchResult(p.Name!, p.Rxcui!))
                .ToList();
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch
        {
            return [];
        }
    }
}
