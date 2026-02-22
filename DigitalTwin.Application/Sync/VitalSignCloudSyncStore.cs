using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Application.Sync;

public class VitalSignCloudSyncStore : ICloudSyncStore<VitalSign>
{
    private readonly IServiceProvider _sp;

    public VitalSignCloudSyncStore(IServiceProvider sp)
    {
        _sp = sp;
    }

    private IVitalSignRepository CloudRepo =>
        _sp.GetKeyedService<IVitalSignRepository>("Cloud") ?? throw new InvalidOperationException("Cloud VitalSign repository not registered.");

    public async Task AddAsync(VitalSign item)
    {
        await CloudRepo.AddAsync(item);
    }

    public async Task<bool> ExistsAsync(VitalSign item)
    {
        return await CloudRepo.ExistsAsync(item.PatientId, item.Type, item.Timestamp);
    }
}
