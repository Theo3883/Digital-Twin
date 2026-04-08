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
        PatientProfileUpdate update)
    {
        if (!string.IsNullOrWhiteSpace(update.Cnp) && update.UserDateOfBirth.HasValue
            && !CnpValidator.MatchesDateOfBirth(update.Cnp, update.UserDateOfBirth.Value))
        {
            throw new DomainException(
                "The date of birth embedded in the CNP does not match the patient's date of birth.");
        }

        var existing = await _patientRepo.GetByUserIdAsync(userId);

        if (existing is not null)
        {
            existing.BloodType           = update.BloodType;
            existing.Allergies           = update.Allergies;
            existing.MedicalHistoryNotes = update.MedicalHistoryNotes;
            existing.Weight = update.Weight;
            existing.Height = update.Height;
            existing.BloodPressureSystolic = update.BloodPressureSystolic;
            existing.BloodPressureDiastolic = update.BloodPressureDiastolic;
            existing.Cholesterol = update.Cholesterol;
            existing.Cnp = update.Cnp;
            await _patientRepo.UpdateAsync(existing);
            return existing;
        }

        var patient = new Patient
        {
            UserId              = userId,
            BloodType           = update.BloodType,
            Allergies           = update.Allergies,
            MedicalHistoryNotes = update.MedicalHistoryNotes,
            Weight = update.Weight,
            Height = update.Height,
            BloodPressureSystolic = update.BloodPressureSystolic,
            BloodPressureDiastolic = update.BloodPressureDiastolic,
            Cholesterol = update.Cholesterol,
            Cnp = update.Cnp
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
