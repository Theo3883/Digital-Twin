using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Application.Sync;

public class PatientCloudSyncStore : ICloudSyncStore<Patient>
{
    private readonly IServiceProvider _sp;

    public PatientCloudSyncStore(IServiceProvider sp) => _sp = sp;

    private IPatientRepository CloudRepo =>
        _sp.GetKeyedService<IPatientRepository>("Cloud") ?? throw new InvalidOperationException("Cloud Patient repository not registered.");

    public async Task AddAsync(Patient item)
    {
        var existing = await CloudRepo.GetByUserIdAsync(item.UserId);
        if (existing is not null)
        {
            existing.BloodType = item.BloodType;
            existing.Allergies = item.Allergies;
            existing.MedicalHistoryNotes = item.MedicalHistoryNotes;
            await CloudRepo.UpdateAsync(existing);
            return;
        }
        await CloudRepo.AddAsync(item);
    }

    public async Task<bool> ExistsAsync(Patient item)
    {
        return await CloudRepo.ExistsAsync(item);
    }
}
