using IOSHealthApp.Domain.Enums;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Interfaces;

public interface IVitalSignRepository
{
    Task<IEnumerable<VitalSign>> GetByPatientAsync(
        long patientId,
        VitalSignType? type = null,
        DateTime? from = null,
        DateTime? to = null);

    Task AddAsync(VitalSign vitalSign);
}
