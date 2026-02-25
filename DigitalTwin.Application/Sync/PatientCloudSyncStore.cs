using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync;

public class PatientCloudSyncStore : ICloudSyncStore<Patient>
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PatientCloudSyncStore> _logger;

    public PatientCloudSyncStore(IServiceProvider sp, ILogger<PatientCloudSyncStore> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    private IPatientRepository CloudRepo =>
        _sp.GetKeyedService<IPatientRepository>("Cloud") ?? throw new InvalidOperationException("Cloud Patient repository not registered.");

    public async Task AddAsync(Patient item)
    {
        _logger.LogInformation("[CloudSync:Patient] AddAsync called. PatientId={Id}, UserId={UserId}", item.Id, item.UserId);

        var existing = await CloudRepo.GetByUserIdAsync(item.UserId);
        if (existing is not null)
        {
            _logger.LogInformation("[CloudSync:Patient] Existing patient found in cloud (Id={Id}). Updating.", existing.Id);
            existing.BloodType = item.BloodType;
            existing.Allergies = item.Allergies;
            existing.MedicalHistoryNotes = item.MedicalHistoryNotes;
            await CloudRepo.UpdateAsync(existing);
            await CloudRepo.MarkSyncedAsync([existing]);
            _logger.LogInformation("[CloudSync:Patient] Updated and marked synced in cloud.");
            return;
        }

        _logger.LogInformation("[CloudSync:Patient] Inserting new patient into cloud.");
        await CloudRepo.AddAsync(item);
        await CloudRepo.MarkSyncedAsync([item]);
        _logger.LogInformation("[CloudSync:Patient] Inserted and marked synced in cloud.");
    }

    public async Task<bool> ExistsAsync(Patient item)
    {
        var exists = await CloudRepo.ExistsAsync(item);
        _logger.LogDebug("[CloudSync:Patient] ExistsAsync UserId={UserId} -> {Exists}", item.UserId, exists);
        return exists;
    }
}
