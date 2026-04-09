using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Domain interface for cloud synchronization - implemented by Infrastructure
/// </summary>
public interface ICloudSyncService
{
    // Authentication
    Task<bool> AuthenticateAsync(string googleIdToken);
    Task<User?> GetCurrentUserProfileAsync();
    
    // User sync
    Task<bool> SyncUserAsync(User user);
    
    // Patient sync  
    Task<bool> SyncPatientAsync(Patient patient);
    Task<Patient?> GetPatientProfileAsync();
    
    // Vital signs sync
    Task<bool> SyncVitalSignsAsync(IEnumerable<VitalSign> vitalSigns);
    Task<IEnumerable<VitalSign>> GetVitalSignsAsync(DateTime fromDate, DateTime? toDate = null);
    
    // Medications sync
    Task<bool> SyncMedicationsAsync(IEnumerable<Medication> medications);
    Task<IEnumerable<Medication>> GetMedicationsAsync();
    
    // Sleep sync
    Task<bool> SyncSleepSessionsAsync(IEnumerable<SleepSession> sessions);
    
    // Environment sync
    Task<bool> SyncEnvironmentReadingsAsync(IEnumerable<EnvironmentReading> readings);
    
    // OCR documents sync
    Task<bool> SyncOcrDocumentsAsync(IEnumerable<OcrDocument> documents);
    
    // Medical history sync
    Task<bool> SyncMedicalHistoryAsync(IEnumerable<MedicalHistoryEntry> entries);
}