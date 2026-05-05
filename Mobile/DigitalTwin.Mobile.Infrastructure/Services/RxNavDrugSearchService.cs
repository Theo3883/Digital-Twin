using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public class RxNavDrugSearchService : IDrugSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RxNavDrugSearchService> _logger;

    public RxNavDrugSearchService(HttpClient httpClient, ILogger<RxNavDrugSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<DrugSearchResult>> SearchByNameAsync(string query, int maxResults = 8, CancellationToken ct = default)
    {
        try
        {
            var url = $"/REST/drugs.json?name={Uri.EscapeDataString(query)}";
            await using var stream = await _httpClient.GetStreamAsync(url, ct);
            var response = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.RxNavDrugsResponse, ct);

            if (response?.DrugGroup?.ConceptGroup == null)
                return [];

            return response.DrugGroup.ConceptGroup
                .Where(g => g.ConceptProperties != null)
                .SelectMany(g => g.ConceptProperties!)
                .Select(p => new DrugSearchResult(p.Name, p.Rxcui))
                .Take(maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RxNav] Drug search failed for query: {Query}", query);
            return [];
        }
    }
}

// RxNav JSON models
public sealed record RxNavDrugsResponse
{
    [JsonPropertyName("drugGroup")]
    public RxNavDrugGroup? DrugGroup { get; init; }
}

public sealed record RxNavDrugGroup
{
    [JsonPropertyName("conceptGroup")]
    public List<RxNavConceptGroup>? ConceptGroup { get; init; }
}

public sealed record RxNavConceptGroup
{
    [JsonPropertyName("conceptProperties")]
    public List<RxNavConceptProperty>? ConceptProperties { get; init; }
}

public sealed record RxNavConceptProperty
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("rxcui")]
    public string Rxcui { get; init; } = string.Empty;
}
