using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;

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
        long userId, string? bloodType, string? allergies, string? medicalHistoryNotes)
    {
        var existing = await _patientRepo.GetByUserIdAsync(userId);

        if (existing is not null)
        {
            existing.BloodType           = bloodType;
            existing.Allergies           = allergies;
            existing.MedicalHistoryNotes = medicalHistoryNotes;
            await _patientRepo.UpdateAsync(existing);
            return existing;
        }

        var patient = new Patient
        {
            UserId              = userId,
            BloodType           = bloodType,
            Allergies           = allergies,
            MedicalHistoryNotes = medicalHistoryNotes
        };
        await _patientRepo.AddAsync(patient);
        return patient;
    }

    /// <summary>
    /// Returns the patient profile for a given user, or null if none exists.
    /// </summary>
    public async Task<Patient?> GetByUserIdAsync(long userId)
    {
        return await _patientRepo.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Returns whether the given user has a patient profile.
    /// </summary>
    public async Task<bool> HasPatientProfileAsync(long userId)
    {
        var patient = await _patientRepo.GetByUserIdAsync(userId);
        return patient is not null;
    }

    /// <summary>
    /// Returns the patient ID for the given user, or null if no profile exists.
    /// </summary>
    public async Task<long?> GetPatientIdForUserAsync(long userId)
    {
        var patient = await _patientRepo.GetByUserIdAsync(userId);
        return patient?.Id;
    }
}
