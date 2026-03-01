using System.Net.Http.Json;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using DigitalTwin.Integrations.Medication.DTOs;

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

            var results = response.FullInteractionTypeGroup
                .Where(g => g.FullInteractionType is not null)
                .SelectMany(g => g.FullInteractionType!)
                .Where(it => it.InteractionPair is not null)
                .SelectMany(it => it.InteractionPair!)
                .Where(pair => pair.InteractionConcept is not null && pair.InteractionConcept.Count >= 2)
                .Select(pair => new MedicationInteraction
                {
                    DrugARxCui = pair.InteractionConcept![0].MinConceptItem?.Rxcui ?? string.Empty,
                    DrugBRxCui = pair.InteractionConcept[1].MinConceptItem?.Rxcui ?? string.Empty,
                    Severity = ParseSeverity(pair.Severity),
                    Description = pair.Description ?? string.Empty
                })
                .ToList();

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
}
