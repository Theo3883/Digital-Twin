using System.Net.Http.Json;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Integrations.Medication;

public class RxNavProvider : IMedicationInteractionProvider
{
    private readonly HttpClient _http;

    public RxNavProvider(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<MedicationInteraction>> GetInteractionsAsync(
        IEnumerable<string> rxCuis)
    {
        var cuiList = rxCuis.ToList();
        if (cuiList.Count < 2)
            return [];

        try
        {
            var joined = string.Join("+", cuiList);
            var url = $"https://rxnav.nlm.nih.gov/REST/interaction/list.json?rxcuis={joined}";
            var response = await _http.GetFromJsonAsync<RxNavInteractionResponse>(url);

            if (response?.FullInteractionTypeGroup is null)
                return [];

            var results = new List<MedicationInteraction>();

            foreach (var group in response.FullInteractionTypeGroup)
            {
                if (group.FullInteractionType is null) continue;

                foreach (var interactionType in group.FullInteractionType)
                {
                    if (interactionType.InteractionPair is null) continue;

                    foreach (var pair in interactionType.InteractionPair)
                    {
                        var concepts = pair.InteractionConcept;
                        if (concepts is null || concepts.Count < 2) continue;

                        results.Add(new MedicationInteraction
                        {
                            DrugARxCui = concepts[0].MinConceptItem?.Rxcui ?? string.Empty,
                            DrugBRxCui = concepts[1].MinConceptItem?.Rxcui ?? string.Empty,
                            Severity = ParseSeverity(pair.Severity),
                            Description = pair.Description ?? string.Empty
                        });
                    }
                }
            }

            return results;
        }
        catch
        {
            return [];
        }
    }

    private static InteractionSeverity ParseSeverity(string? severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "high" => InteractionSeverity.High,
            "n/a" => InteractionSeverity.Medium,
            _ => InteractionSeverity.Low
        };
    }

    private class RxNavInteractionResponse
    {
        public List<FullInteractionTypeGroup>? FullInteractionTypeGroup { get; set; }
    }

    private class FullInteractionTypeGroup
    {
        public List<FullInteractionType>? FullInteractionType { get; set; }
    }

    private class FullInteractionType
    {
        public List<InteractionPair>? InteractionPair { get; set; }
    }

    private class InteractionPair
    {
        public string? Severity { get; set; }
        public string? Description { get; set; }
        public List<InteractionConcept>? InteractionConcept { get; set; }
    }

    private class InteractionConcept
    {
        public MinConceptItem? MinConceptItem { get; set; }
    }

    private class MinConceptItem
    {
        public string? Rxcui { get; set; }
        public string? Name { get; set; }
    }
}
