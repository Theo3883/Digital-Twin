using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

/// <summary>
/// Application service for patient profile management
/// </summary>
public class PatientService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PatientService> _logger;

    public PatientService(
        IPatientRepository patientRepository,
        IUserRepository userRepository,
        ILogger<PatientService> logger)
    {
        _patientRepository = patientRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets current patient profile
    /// </summary>
    public async Task<PatientDto?> GetCurrentPatientAsync()
    {
        var patient = await _patientRepository.GetCurrentPatientAsync();
        if (patient == null) return null;

        return new PatientDto
        {
            Id = patient.Id,
            UserId = patient.UserId,
            BloodType = patient.BloodType,
            Allergies = patient.Allergies,
            MedicalHistoryNotes = patient.MedicalHistoryNotes,
            Weight = patient.Weight,
            Height = patient.Height,
            BloodPressureSystolic = patient.BloodPressureSystolic,
            BloodPressureDiastolic = patient.BloodPressureDiastolic,
            Cholesterol = patient.Cholesterol,
            Cnp = patient.Cnp,
            IsSynced = patient.IsSynced
        };
    }

    /// <summary>
    /// Updates patient profile
    /// </summary>
    public async Task<bool> UpdatePatientAsync(PatientUpdateInput input)
    {
        try
        {
            var patient = await _patientRepository.GetCurrentPatientAsync();
            if (patient == null)
            {
                var user = await _userRepository.GetCurrentUserAsync();
                if (user == null)
                {
                    _logger.LogWarning("[PatientService] No current user found while creating patient profile");
                    return false;
                }

                patient = new Domain.Models.Patient
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsSynced = false
                };

                _logger.LogInformation("[PatientService] Creating initial patient profile for user {UserId}", user.Id);
            }

            // Update fields
            patient.BloodType = input.BloodType ?? patient.BloodType;
            patient.Allergies = input.Allergies ?? patient.Allergies;
            patient.MedicalHistoryNotes = input.MedicalHistoryNotes ?? patient.MedicalHistoryNotes;
            patient.Weight = input.Weight ?? patient.Weight;
            patient.Height = input.Height ?? patient.Height;
            patient.BloodPressureSystolic = input.BloodPressureSystolic ?? patient.BloodPressureSystolic;
            patient.BloodPressureDiastolic = input.BloodPressureDiastolic ?? patient.BloodPressureDiastolic;
            patient.Cholesterol = input.Cholesterol ?? patient.Cholesterol;
            patient.Cnp = input.Cnp ?? patient.Cnp;
            
            patient.UpdatedAt = DateTime.UtcNow;
            patient.IsSynced = false; // Mark as needing sync

            await _patientRepository.SaveAsync(patient);
            _logger.LogInformation("[PatientService] Updated patient profile");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PatientService] Failed to update patient");
            return false;
        }
    }
}