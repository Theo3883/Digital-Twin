using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Concrete facade that groups Doctor Portal data-access repositories into a single
/// injectable unit. Registered as scoped alongside <see cref="DoctorPortalApplicationService"/>.
/// </summary>
public sealed class DoctorPortalDataFacade : IDoctorPortalDataFacade
{
    public DoctorPortalDataFacade(
        IDoctorPatientAssignmentRepository assignments,
        IPatientRepository patients,
        IUserRepository users,
        IVitalSignRepository vitals,
        ISleepSessionRepository sleep,
        IMedicationRepository medications)
    {
        Assignments = assignments;
        Patients    = patients;
        Users       = users;
        Vitals      = vitals;
        Sleep       = sleep;
        Medications = medications;
    }

    public IDoctorPatientAssignmentRepository Assignments { get; }
    public IPatientRepository                 Patients    { get; }
    public IUserRepository                    Users       { get; }
    public IVitalSignRepository               Vitals      { get; }
    public ISleepSessionRepository            Sleep       { get; }
    public IMedicationRepository              Medications { get; }
}
