using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IMedicationRepository
{
    Task<IEnumerable<Medication>> GetByPatientAsync(Guid patientId);
    Task<Medication?> GetByIdAsync(Guid id);
    Task AddAsync(Medication medication);
    Task AddRangeAsync(IEnumerable<Medication> medications);
    Task UpsertRangeAsync(IEnumerable<Medication> medications);
    Task SoftDeleteAsync(Guid id);
    Task DiscontinueAsync(Guid id, DateTime endDate, string? reason);
    Task UpdateAsync(Medication medication);
    Task<bool> ExistsAsync(Guid id);
    Task<IEnumerable<Medication>> GetDirtyAsync();
    Task MarkSyncedAsync(Guid patientId);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
