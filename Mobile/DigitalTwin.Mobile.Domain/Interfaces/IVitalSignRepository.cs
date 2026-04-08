using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Domain interface for vital sign data access in mobile app
/// </summary>
public interface IVitalSignRepository
{
    Task<VitalSign?> GetByIdAsync(Guid id);
    Task<IEnumerable<VitalSign>> GetByPatientIdAsync(Guid patientId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<VitalSign>> GetByTypeAsync(Guid patientId, VitalSignType type, DateTime? fromDate = null, DateTime? toDate = null);
    Task SaveAsync(VitalSign vitalSign);
    Task SaveRangeAsync(IEnumerable<VitalSign> vitalSigns);
    Task<IEnumerable<VitalSign>> GetUnsyncedAsync();
    Task MarkAsSyncedAsync(IEnumerable<Guid> ids);
}