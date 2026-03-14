using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IMedicationRepository
{
    Task<IEnumerable<Medication>> GetByPatientAsync(Guid patientId);
    Task<Medication?> GetByIdAsync(Guid id);
    Task AddAsync(Medication medication);
    Task SoftDeleteAsync(Guid id);
    Task DiscontinueAsync(Guid id, DateTime endDate, string? reason);
    Task<IEnumerable<Medication>> GetDirtyAsync();
    Task MarkSyncedAsync(Guid patientId);
}
