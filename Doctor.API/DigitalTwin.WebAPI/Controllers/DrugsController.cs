using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Drug search for RxCUI autocomplete (used by doctor portal and MAUI).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DrugsController : ControllerBase
{
    private readonly IMedicationApplicationService _medicationService;

    public DrugsController(IMedicationApplicationService medicationService)
    {
        _medicationService = medicationService;
    }

    /// <summary>GET /api/drugs/search?q=metoprolol — search drugs by name, returns name + RxCUI.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<DrugSearchResultDto>>> Search(
        [FromQuery] string q,
        [FromQuery] int max = 8,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<DrugSearchResultDto>());

        var results = await _medicationService.SearchDrugsByNameAsync(q.Trim(), max, ct);
        return Ok(results);
    }

    /// <summary>
    /// POST /api/drugs/interactions — checks interactions for the supplied RxCUIs.
    /// </summary>
    [HttpPost("interactions")]
    public async Task<ActionResult<IEnumerable<MedicationInteractionDto>>> CheckInteractions(
        [FromBody] DrugInteractionsRequest request,
        CancellationToken ct = default)
    {
        var rxCuis = request.RxCuis
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rxCuis.Count < 2)
            return Ok(Array.Empty<MedicationInteractionDto>());

        var interactions = await _medicationService.CheckInteractionsAsync(rxCuis);
        return Ok(interactions);
    }

    public sealed class DrugInteractionsRequest
    {
        public List<string> RxCuis { get; set; } = [];
    }
}
