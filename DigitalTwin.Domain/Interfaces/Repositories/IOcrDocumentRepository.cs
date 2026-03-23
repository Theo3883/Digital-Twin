namespace DigitalTwin.Domain.Interfaces.Repositories;

using DigitalTwin.Domain.Models;

public interface IOcrDocumentRepository
{
    Task<IEnumerable<OcrDocument>> GetByPatientAsync(Guid patientId);
    Task<OcrDocument?> GetByIdAsync(Guid id);
    Task<IEnumerable<OcrDocument>> GetDirtyAsync();
    Task AddAsync(OcrDocument document);
    Task UpdateAsync(OcrDocument document);
    Task DeleteAsync(Guid id);
    Task MarkSyncedAsync(Guid id);
    Task UpsertRangeAsync(IEnumerable<OcrDocument> documents);
    Task PurgeSyncedOlderThanAsync(DateTime olderThan);
}
