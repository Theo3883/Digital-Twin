using DigitalTwin.Domain.Interfaces.Repositories;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Facade grouping all data-access repositories consumed by the Doctor Portal.
/// Reduces constructor parameter count (<see cref="Services.DoctorPortalApplicationService"/>)
/// and provides a single registration point for the portal's data dependencies.
/// </summary>
public interface IDoctorPortalDataFacade
{
    IDoctorPatientAssignmentRepository Assignments { get; }
    IPatientRepository Patients { get; }
    IUserRepository Users { get; }
    IVitalSignRepository Vitals { get; }
    ISleepSessionRepository Sleep { get; }
    IMedicationRepository Medications { get; }
}
