using DigitalTwin.Domain.Exceptions;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Validators;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Domain service encapsulating patient profile business logic.
/// Owns the rules for: creating/updating patient medical profiles.
/// </summary>
public class PatientService : IPatientService
{
    private readonly IPatientRepository _patientRepo;

    public PatientService(IPatientRepository patientRepo)
    {
        _patientRepo = patientRepo;
    }

    /// <summary>
    /// Creates a new patient profile or updates an existing one for the given user.
    /// </summary>
    public async Task<Patient> CreateOrUpdateProfileAsync(
        Guid userId,
        string? bloodType,
        string? allergies,
        string? medicalHistoryNotes,
        decimal? weight,
        decimal? height,
        int? bloodPressureSystolic,
        int? bloodPressureDiastolic,
        decimal? cholesterol,
        string? cnp,
        DateTime? userDateOfBirth)
    {
        if (!string.IsNullOrWhiteSpace(cnp) && userDateOfBirth.HasValue)
        {
            if (!CnpValidator.MatchesDateOfBirth(cnp, userDateOfBirth.Value))
                throw new DomainException(
                    "The date of birth embedded in the CNP does not match the patient's date of birth.");
        }

        var existing = await _patientRepo.GetByUserIdAsync(userId);

        if (existing is not null)
        {
            existing.BloodType           = bloodType;
            existing.Allergies           = allergies;
            existing.MedicalHistoryNotes = medicalHistoryNotes;
            existing.Weight = weight;
            existing.Height = height;
            existing.BloodPressureSystolic = bloodPressureSystolic;
            existing.BloodPressureDiastolic = bloodPressureDiastolic;
            existing.Cholesterol = cholesterol;
            existing.Cnp = cnp;
            await _patientRepo.UpdateAsync(existing);
            return existing;
        }

        var patient = new Patient
        {
            UserId              = userId,
            BloodType           = bloodType,
            Allergies           = allergies,
            MedicalHistoryNotes = medicalHistoryNotes,
            Weight = weight,
            Height = height,
            BloodPressureSystolic = bloodPressureSystolic,
            BloodPressureDiastolic = bloodPressureDiastolic,
            Cholesterol = cholesterol,
            Cnp = cnp
        };
        await _patientRepo.AddAsync(patient);
        return patient;
    }

    /// <summary>
    /// Returns the patient profile for a given user, or null if none exists.
    /// </summary>
    public async Task<Patient?> GetByUserIdAsync(Guid userId)
    {
        return await _patientRepo.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Returns whether the given user has a patient profile.
    /// </summary>
    public async Task<bool> HasPatientProfileAsync(Guid userId)
    {
        var patient = await _patientRepo.GetByUserIdAsync(userId);
        return patient is not null;
    }

    /// <summary>
    /// Returns the patient ID for the given user, or null if no profile exists.
    /// </summary>
    public async Task<Guid?> GetPatientIdForUserAsync(Guid userId)
    {
        var patient = await _patientRepo.GetByUserIdAsync(userId);
        return patient?.Id;
    }
}
