using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IMedicalHistoryEntryRepository
{
    Task<IEnumerable<MedicalHistoryEntry>> GetByPatientAsync(Guid patientId);
    Task<IEnumerable<MedicalHistoryEntry>> GetBySourceDocumentAsync(Guid sourceDocumentId);
    Task<IEnumerable<MedicalHistoryEntry>> GetDirtyAsync();
    Task AddRangeAsync(IEnumerable<MedicalHistoryEntry> entries);
    Task UpsertRangeAsync(IEnumerable<MedicalHistoryEntry> entries);
    Task MarkSyncedAsync(IEnumerable<Guid> ids);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}

