using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DigitalTwin.Mobile.Application.Services;

public class MedicationApplicationService
{
    private readonly IMedicationRepository _medicationRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IDrugSearchProvider _drugSearch;
    private readonly IMedicationInteractionProvider _interactionProvider;
    private readonly MedicationService _medicationService;
    private readonly MedicationInteractionService _interactionService;
    private readonly ILogger<MedicationApplicationService> _logger;

    public MedicationApplicationService(
        IMedicationRepository medicationRepo,
        IPatientRepository patientRepo,
        IDrugSearchProvider drugSearch,
        IMedicationInteractionProvider interactionProvider,
        MedicationService medicationService,
        MedicationInteractionService interactionService,
        ILogger<MedicationApplicationService> logger)
    {
        _medicationRepo = medicationRepo;
        _patientRepo = patientRepo;
        _drugSearch = drugSearch;
        _interactionProvider = interactionProvider;
        _medicationService = medicationService;
        _interactionService = interactionService;
        _logger = logger;
    }

    public async Task<IEnumerable<MedicationDto>> GetMedicationsAsync()
    {
        var patient = await _patientRepo.GetCurrentPatientAsync();
        if (patient == null) return [];

        var medications = await _medicationRepo.GetByPatientIdAsync(patient.Id);
        return medications.Select(MapToDto);
    }

    public async Task<(bool Success, string? Error)> AddMedicationAsync(AddMedicationInput input)
    {
        try
        {
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient == null)
                return (false, "No patient profile found");

            var resolvedRxCui = await ResolveBestRxCuiAsync(input.RxCui, input.Name);

            var request = new CreateMedicationRequest(
                PatientId: patient.Id,
                Name: input.Name,
                Dosage: input.Dosage,
                Frequency: input.Frequency,
                Route: input.Route,
                RxCui: resolvedRxCui,
                Instructions: input.Instructions,
                Reason: input.Reason,
                PrescribedByUserId: null,
                StartDate: input.StartDate,
                AddedByRole: AddedByRole.Patient);

            var medication = _medicationService.CreateMedication(request);

            // Check interactions if RxCUI available
            if (!string.IsNullOrEmpty(medication.RxCui))
            {
                var activeMeds = await _medicationRepo.GetActiveByPatientIdAsync(patient.Id);
                var existingRxCuis = activeMeds
                    .Where(m => !string.IsNullOrEmpty(m.RxCui))
                    .Select(m => m.RxCui!.Trim())
                    .ToList();

                var unresolvedActiveNames = activeMeds
                    .Where(m => string.IsNullOrWhiteSpace(m.RxCui) && !string.IsNullOrWhiteSpace(m.Name))
                    .Select(m => m.Name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var unresolvedName in unresolvedActiveNames)
                {
                    var resolvedActiveRxCui = await ResolveBestRxCuiAsync(null, unresolvedName);
                    if (!string.IsNullOrWhiteSpace(resolvedActiveRxCui))
                        existingRxCuis.Add(resolvedActiveRxCui);
                }

                var rxCuis = existingRxCuis
                    .Append(medication.RxCui)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (rxCuis.Count > 1)
                {
                    var interactions = (await _interactionProvider.GetInteractionsAsync(rxCuis)).ToList();
                    var newMedicationInteractions = interactions
                        .Where(i => i.DrugARxCui.Equals(medication.RxCui, StringComparison.OrdinalIgnoreCase)
                                 || i.DrugBRxCui.Equals(medication.RxCui, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (_interactionService.IsAdditionBlocked(newMedicationInteractions))
                    {
                        var blocking = _interactionService.GetBlockingInteractions(newMedicationInteractions);
                        var desc = string.Join("; ", blocking.Select(i => i.Description));
                        return (false, $"High-risk interaction detected: {desc}");
                    }
                }
            }

            await _medicationRepo.SaveAsync(medication);
            _logger.LogInformation("[MedicationApp] Added medication: {Name}", medication.Name);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MedicationApp] Failed to add medication");
            return (false, ex.Message);
        }
    }

    /// <summary>OCR pipeline: add medication without blocking on interaction checks (MAUI parity).</summary>
    public async Task<(bool Success, string? Error)> AddMedicationFromOcrAsync(AddMedicationInput input)
    {
        try
        {
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient == null)
                return (false, "No patient profile found");

            var request = new CreateMedicationRequest(
                PatientId: patient.Id,
                Name: input.Name,
                Dosage: input.Dosage,
                Frequency: input.Frequency,
                Route: input.Route,
                RxCui: input.RxCui,
                Instructions: input.Instructions,
                Reason: input.Reason,
                PrescribedByUserId: null,
                StartDate: input.StartDate,
                AddedByRole: AddedByRole.OcrScan);

            var medication = _medicationService.CreateMedication(request);
            await _medicationRepo.SaveAsync(medication);
            _logger.LogInformation("[MedicationApp] OCR-added medication: {Name}", medication.Name);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MedicationApp] OCR auto-add failed for {Name}", input.Name);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DiscontinueMedicationAsync(DiscontinueMedicationInput input)
    {
        try
        {
            var patient = await _patientRepo.GetCurrentPatientAsync();
            if (patient == null)
                return (false, "No patient profile found");

            var medication = await _medicationRepo.GetByIdAsync(input.MedicationId);
            if (medication == null)
                return (false, "Medication not found");

            _medicationService.ValidateOwnership(patient.Id, medication);
            _medicationService.Discontinue(medication, input.Reason);
            await _medicationRepo.UpdateAsync(medication);

            _logger.LogInformation("[MedicationApp] Discontinued medication: {Id}", input.MedicationId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MedicationApp] Failed to discontinue medication");
            return (false, ex.Message);
        }
    }

    public async Task<IEnumerable<DrugSearchResultDto>> SearchDrugsAsync(string query)
    {
        var results = await _drugSearch.SearchByNameAsync(query);
        return results.Select(r => new DrugSearchResultDto { Name = r.Name, RxCui = r.RxCui });
    }

    public async Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(IEnumerable<string> rxCuis)
    {
        var interactions = await _interactionProvider.GetInteractionsAsync(rxCuis);
        return interactions.Select(i => new MedicationInteractionDto
        {
            DrugARxCui = i.DrugARxCui,
            DrugBRxCui = i.DrugBRxCui,
            Severity = (int)i.Severity,
            Description = i.Description
        });
    }

    private async Task<string?> ResolveBestRxCuiAsync(string? existingRxCui, string? medicationName)
    {
        if (!string.IsNullOrWhiteSpace(existingRxCui))
            return existingRxCui.Trim();

        if (string.IsNullOrWhiteSpace(medicationName))
            return null;

        var canonicalName = medicationName.Trim();
        var normalizedInput = NormalizeForMatch(canonicalName);
        DrugSearchResult? best = null;
        var bestScore = int.MinValue;

        foreach (var query in BuildSearchQueries(canonicalName))
        {
            var candidates = (await _drugSearch.SearchByNameAsync(query, maxResults: 20)).ToList();
            if (candidates.Count == 0)
                continue;

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.RxCui))
                    continue;

                var score = GetMatchScore(normalizedInput, NormalizeForMatch(candidate.Name));
                if (score > bestScore ||
                    (score == bestScore && candidate.Name.Length < (best?.Name.Length ?? int.MaxValue)))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (bestScore >= 90)
                break;
        }

        return best?.RxCui?.Trim();
    }

    private static IEnumerable<string> BuildSearchQueries(string name)
    {
        var queries = new List<string>();

        static void AddQuery(List<string> values, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.OrdinalIgnoreCase))
                values.Add(value);
        }

        AddQuery(queries, name.Trim());

        var withoutDiacritics = RemoveDiacritics(name.Trim());
        AddQuery(queries, withoutDiacritics);

        var cleaned = Regex.Replace(withoutDiacritics, @"[^a-zA-Z0-9\s-]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        AddQuery(queries, cleaned);

        var firstToken = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstToken) && firstToken.Length >= 4)
            AddQuery(queries, firstToken);

        if (!string.IsNullOrWhiteSpace(firstToken) && firstToken.EndsWith("a", StringComparison.OrdinalIgnoreCase) && firstToken.Length > 6)
            AddQuery(queries, firstToken[..^1]);

        return queries;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeForMatch(string value)
    {
        var normalized = RemoveDiacritics(value).Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static int GetMatchScore(string input, string candidate)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(candidate))
            return 0;

        if (candidate.Equals(input, StringComparison.Ordinal)) return 100;
        if (candidate.StartsWith(input + " ", StringComparison.Ordinal)) return 90;
        if (candidate.Contains(" " + input + " ", StringComparison.Ordinal)
            || candidate.EndsWith(" " + input, StringComparison.Ordinal)) return 80;
        if (candidate.Contains(input, StringComparison.Ordinal)) return 60;

        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var overlap = tokens.Count(t => candidate.Contains(t, StringComparison.Ordinal));
        return overlap * 10;
    }

    private static MedicationDto MapToDto(Medication m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Dosage = m.Dosage,
        Frequency = m.Frequency,
        Route = m.Route,
        Status = m.Status,
        RxCui = m.RxCui,
        Instructions = m.Instructions,
        Reason = m.Reason,
        PrescribedByUserId = m.PrescribedByUserId,
        StartDate = m.StartDate,
        EndDate = m.EndDate,
        DiscontinuedReason = m.DiscontinuedReason,
        AddedByRole = m.AddedByRole,
        CreatedAt = m.CreatedAt,
        IsSynced = m.IsSynced
    };
}
