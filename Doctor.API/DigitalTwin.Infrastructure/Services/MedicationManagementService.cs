using DigitalTwin.Domain.Exceptions;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IMedicationManagementService"/>.
/// Applies cloud-first / local-fallback persistence strategy for write operations.
/// Read operations target the primary (local) repository.
/// </summary>
public sealed class MedicationManagementService : IMedicationManagementService
{
    private readonly IMedicationRepository     _local;
    private readonly IMedicationRepository?    _cloud;
    private readonly IMedicationService        _medicationService;
    private readonly ILogger<MedicationManagementService> _logger;

    public MedicationManagementService(
        IMedicationRepository local,
        IMedicationRepository? cloud,
        IMedicationService medicationService,
        ILogger<MedicationManagementService> logger)
    {
        _local             = local;
        _cloud             = cloud;
        _medicationService = medicationService;
        _logger            = logger;
    }

    public async Task<IEnumerable<Medication>> GetByPatientAsync(
        Guid patientId, CancellationToken ct = default)
        => await _local.GetByPatientAsync(patientId);

    public async Task<Medication?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
        => await _local.GetByIdAsync(id);

    public async Task AddAsync(Medication medication, CancellationToken ct = default)
    {
        var cloudSucceeded = false;
        if (_cloud is not null)
        {
            try
            {
                await _cloud.AddAsync(medication, markDirty: false);
                cloudSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Medication] Cloud write failed; will sync later via drain cycle.");
            }
        }

        await _local.AddAsync(medication, markDirty: !cloudSucceeded);
    }

    public async Task SoftDeleteAsync(
        Guid patientId, Guid medicationId, CancellationToken ct = default)
    {
        var medication = await _local.GetByIdAsync(medicationId)
            ?? throw new NotFoundException(nameof(Medication), medicationId);

        _medicationService.ValidateOwnership(patientId, medication);

        await _local.SoftDeleteAsync(medicationId);
    }

    public async Task DiscontinueAsync(
        Guid patientId, Guid medicationId, string? reason, CancellationToken ct = default)
    {
        var medication = await _local.GetByIdAsync(medicationId)
            ?? throw new NotFoundException(nameof(Medication), medicationId);

        _medicationService.ValidateOwnership(patientId, medication);

        var now = DateTime.UtcNow;
        await _local.DiscontinueAsync(medicationId, now, reason?.Trim());
    }
}
