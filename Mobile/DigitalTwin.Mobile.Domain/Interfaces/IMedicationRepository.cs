using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IMedicationRepository
{
    Task<IEnumerable<Medication>> GetByPatientIdAsync(Guid patientId);
    Task<IEnumerable<Medication>> GetActiveByPatientIdAsync(Guid patientId);
    Task<Medication?> GetByIdAsync(Guid id);
    Task SaveAsync(Medication medication);
    Task UpdateAsync(Medication medication);
    Task<IEnumerable<Medication>> GetUnsyncedAsync();
    Task MarkAsSyncedAsync(Guid patientId);
}
