using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Services;
using Microsoft.Extensions.Logging;

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
                AddedByRole: AddedByRole.Patient);

            var medication = _medicationService.CreateMedication(request);

            // Check interactions if RxCUI available
            if (!string.IsNullOrEmpty(medication.RxCui))
            {
                var activeMeds = await _medicationRepo.GetActiveByPatientIdAsync(patient.Id);
                var rxCuis = activeMeds
                    .Where(m => !string.IsNullOrEmpty(m.RxCui))
                    .Select(m => m.RxCui!)
                    .Append(medication.RxCui)
                    .Distinct()
                    .ToList();

                if (rxCuis.Count > 1)
                {
                    var interactions = await _interactionProvider.GetInteractionsAsync(rxCuis);
                    if (_interactionService.IsAdditionBlocked(interactions))
                    {
                        var blocking = _interactionService.GetBlockingInteractions(interactions);
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
            Severity = i.Severity.ToString(),
            Description = i.Description
        });
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
