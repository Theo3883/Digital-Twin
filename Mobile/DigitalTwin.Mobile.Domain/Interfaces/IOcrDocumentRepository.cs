using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IOcrDocumentRepository
{
    Task<IEnumerable<OcrDocument>> GetByPatientIdAsync(Guid patientId);
    Task<OcrDocument?> GetByIdAsync(Guid id);
    Task SaveAsync(OcrDocument document);
    Task UpdateAsync(OcrDocument document);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<OcrDocument>> GetDirtyAsync();
    Task MarkSyncedAsync(Guid id);
}
