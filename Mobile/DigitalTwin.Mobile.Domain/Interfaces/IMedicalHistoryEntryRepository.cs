using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IMedicalHistoryEntryRepository
{
    Task<IEnumerable<MedicalHistoryEntry>> GetByPatientIdAsync(Guid patientId);
    Task<IEnumerable<MedicalHistoryEntry>> GetBySourceDocumentIdAsync(Guid sourceDocumentId);
    Task SaveRangeAsync(IEnumerable<MedicalHistoryEntry> entries);
    Task DeleteBySourceDocumentIdAsync(Guid sourceDocumentId);
    Task<IEnumerable<MedicalHistoryEntry>> GetDirtyAsync();
    Task MarkSyncedAsync(IEnumerable<Guid> ids);
}
