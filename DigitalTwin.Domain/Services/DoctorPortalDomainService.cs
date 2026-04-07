using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Exceptions;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Domain service for all Doctor Portal business operations.
///
/// Design patterns:
///   Guard Clauses — every method validates preconditions before executing.
///   Factory Method — <see cref="AssignPatientAsync"/> is the only place that
///                    constructs <see cref="DoctorPatientAssignment"/> objects.
///   Observer       — domain events are dispatched after state changes so
///                    the application layer can trigger side-effects (sync, etc.)
///                    without introducing coupling into the domain.
/// </summary>
public sealed class DoctorPortalDomainService : IDoctorPortalDomainService
{
    private readonly IUserRepository                      _users;
    private readonly IPatientRepository                   _patients;
    private readonly IDoctorPatientAssignmentRepository   _assignments;
    private readonly IVitalSignRepository                 _vitals;
    private readonly ISleepSessionRepository              _sleep;
    private readonly IMedicationRepository                _medications;
    private readonly IMedicationService                   _medicationFactory;
    private readonly IDomainEventDispatcher               _events;

    public sealed record Repositories(
        IUserRepository Users,
        IPatientRepository Patients,
        IDoctorPatientAssignmentRepository Assignments,
        IVitalSignRepository Vitals,
        ISleepSessionRepository Sleep,
        IMedicationRepository Medications);

    public DoctorPortalDomainService(
        Repositories repos,
        IMedicationService medicationFactory,
        IDomainEventDispatcher events)
    {
        _users             = repos.Users;
        _patients          = repos.Patients;
        _assignments       = repos.Assignments;
        _vitals            = repos.Vitals;
        _sleep             = repos.Sleep;
        _medications       = repos.Medications;
        _medicationFactory = medicationFactory;
        _events            = events;
    }

    // ── Authorization ────────────────────────────────────────────────────────

    public async Task<User> GetDoctorByEmailAsync(string doctorEmail, CancellationToken ct = default)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null)
            throw new NotFoundException(nameof(User), doctorEmail);
        return doctor;
    }

    public async Task<Guid> RequireAuthorizedDoctorIdAsync(
        string doctorEmail, Guid patientId, CancellationToken ct = default)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null)
            throw new UnauthorizedException($"Doctor with email '{doctorEmail}' does not exist.");

        if (!await _assignments.IsAssignedAsync(doctor.Id, patientId))
            throw new UnauthorizedException(
                $"Doctor '{doctorEmail}' is not authorised to access patient '{patientId}'.");

        return doctor.Id;
    }

    // ── Patient lookup ───────────────────────────────────────────────────────

    public async Task<IEnumerable<DoctorPatientAssignment>> GetAssignmentsForDoctorAsync(
        Guid doctorId, CancellationToken ct = default)
        => await _assignments.GetByDoctorIdAsync(doctorId);

    public async Task<(User user, Patient patient)> GetPatientWithUserAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var patient = await _patients.GetByIdAsync(patientId)
            ?? throw new NotFoundException(nameof(Patient), patientId);

        var user = await _users.GetByIdAsync(patient.UserId)
            ?? throw new NotFoundException(nameof(User), patient.UserId);

        return (user, patient);
    }

    // ── Assignment factory ───────────────────────────────────────────────────

    public async Task<DoctorPatientAssignment> AssignPatientAsync(
        string doctorEmail, string patientEmail, string? notes, CancellationToken ct = default)
    {
        // Guard: doctor exists
        var doctor = await _users.GetByEmailAsync(doctorEmail)
            ?? throw new NotFoundException($"Doctor with email '{doctorEmail}' was not found.");

        // Guard: patient user exists
        var patientUser = await _users.GetByEmailAsync(patientEmail)
            ?? throw new NotFoundException($"Patient with email '{patientEmail}' was not found.");

        // Guard: patient profile exists
        var patient = await _patients.GetByUserIdAsync(patientUser.Id)
            ?? throw new NotFoundException($"No patient profile found for email '{patientEmail}'.");

        // Guard: not already assigned
        if (await _assignments.IsAssignedAsync(doctor.Id, patient.Id))
            throw new DuplicateAssignmentException(doctor.Id, patient.Id);

        // Factory: create the assignment
        var assignment = new DoctorPatientAssignment
        {
            DoctorId           = doctor.Id,
            PatientId          = patient.Id,
            PatientEmail       = patientEmail,
            AssignedByDoctorId = doctor.Id,
            Notes              = notes?.Trim()
        };

        await _assignments.AddAsync(assignment);

        await _events.DispatchAsync(
            new PatientAssignedEvent(doctor.Id, patient.Id, patientEmail), ct);

        return assignment;
    }

    public async Task UnassignPatientAsync(
        string doctorEmail, Guid patientId, CancellationToken ct = default)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail)
            ?? throw new NotFoundException($"Doctor with email '{doctorEmail}' was not found.");

        if (!await _assignments.IsAssignedAsync(doctor.Id, patientId))
            return; // idempotent — already unassigned

        await _assignments.RemoveAsync(doctor.Id, patientId);

        await _events.DispatchAsync(new PatientUnassignedEvent(doctor.Id, patientId), ct);
    }

    // ── Scoped data reads ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<VitalSign>> GetPatientVitalsAsync(
        Guid patientId, VitalSignType? type, DateTime? from, DateTime? to, CancellationToken ct = default)
        => (await _vitals.GetByPatientAsync(patientId, type, from, to)).ToList();

    public async Task<IReadOnlyList<SleepSession>> GetPatientSleepAsync(
        Guid patientId, DateTime? from, DateTime? to, CancellationToken ct = default)
        => (await _sleep.GetByPatientAsync(patientId, from, to)).ToList();

    public async Task<IReadOnlyList<Medication>> GetPatientMedicationsAsync(
        Guid patientId, CancellationToken ct = default)
        => (await _medications.GetByPatientAsync(patientId)).ToList();

    // ── Medication management ────────────────────────────────────────────────

    public async Task<Medication> AddPatientMedicationAsync(
        Guid patientId, CreateMedicationRequest request, CancellationToken ct = default)
    {
        var medication = _medicationFactory.CreateMedication(request);
        await _medications.AddAsync(medication);

        await _events.DispatchAsync(
            new MedicationAddedEvent(patientId, medication.Id, medication.Name), ct);

        return medication;
    }

    public async Task SoftDeleteMedicationAsync(
        Guid patientId, Guid medicationId, CancellationToken ct = default)
    {
        var medication = await _medications.GetByIdAsync(medicationId)
            ?? throw new NotFoundException(nameof(Medication), medicationId);

        _medicationFactory.ValidateOwnership(patientId, medication);

        await _medications.SoftDeleteAsync(medicationId);

        await _events.DispatchAsync(new MedicationDeletedEvent(patientId, medicationId), ct);
    }

    public async Task DiscontinueMedicationAsync(
        Guid patientId, Guid medicationId, string reason, CancellationToken ct = default)
    {
        var medication = await _medications.GetByIdAsync(medicationId)
            ?? throw new NotFoundException(nameof(Medication), medicationId);

        _medicationFactory.ValidateOwnership(patientId, medication);

        var updated = _medicationFactory.Discontinue(medication, reason);
        await _medications.UpdateAsync(updated);

        await _events.DispatchAsync(
            new MedicationDiscontinuedEvent(patientId, medicationId, reason), ct);
    }
}
