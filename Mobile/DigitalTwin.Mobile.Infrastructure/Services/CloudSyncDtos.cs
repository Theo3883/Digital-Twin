namespace DigitalTwin.Mobile.Infrastructure.Services;

// Public DTOs so System.Text.Json source generation can reference them (NativeAOT-safe).
public sealed record GoogleAuthRequest
{
    public string GoogleIdToken { get; init; } = string.Empty;
}

public sealed record DeviceRequestEnvelope<T>
{
    public string DeviceId { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public T? User { get; init; }
    public T? Patient { get; init; }
    public List<T>? Items { get; init; }
}

public sealed record UpsertUserRequest
{
    public string Email { get; init; } = string.Empty;
    public int Role { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Phone { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
}

public sealed record UpsertPatientRequest
{
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public double? Weight { get; init; }
    public double? Height { get; init; }
    public int? BloodPressureSystolic { get; init; }
    public int? BloodPressureDiastolic { get; init; }
    public double? Cholesterol { get; init; }
    public string? Cnp { get; init; }
}

public sealed record VitalAppendRequestItem
{
    public int Type { get; init; }
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public sealed record AuthResponse
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? ErrorMessage { get; init; }
    public MobileBootstrapDto? Bootstrap { get; init; }
}

public sealed record MobileBootstrapDto
{
    public CloudUserDto? User { get; init; }
    public CloudPatientDto? Patient { get; init; }
    public List<CloudVitalSignDto>? Vitals { get; init; }
    public List<CloudMedicationDto>? Medications { get; init; }
    public List<CloudSleepSessionDto>? SleepSessions { get; init; }
    public List<CloudEnvironmentReadingDto>? EnvironmentReadings { get; init; }
    public List<CloudOcrDocumentDto>? OcrDocuments { get; init; }
    public List<CloudMedicalHistoryEntryDto>? MedicalHistoryEntries { get; init; }
}

public sealed record UserProfileResponse
{
    public CloudUserDto? User { get; init; }
}

public sealed record CloudUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public int Role { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public DateTime? DateOfBirth { get; init; }
}

public sealed record SyncResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record PatientProfileResponse
{
    public CloudPatientDto? Patient { get; init; }
}

public sealed record CloudPatientDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public double? Weight { get; init; }
    public double? Height { get; init; }
    public int? BloodPressureSystolic { get; init; }
    public int? BloodPressureDiastolic { get; init; }
    public double? Cholesterol { get; init; }
    public string? Cnp { get; init; }
}

public sealed record VitalSyncResponse
{
    public int AcceptedCount { get; init; }
    public int DedupedCount { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record VitalSignsResponse
{
    public List<CloudVitalSignDto>? Items { get; init; }
}

public sealed record CloudVitalSignDto
{
    public Guid? Id { get; init; }
    public int Type { get; init; }
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public sealed record CloudMedicationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string? Frequency { get; init; }
    public int Route { get; init; }
    public string? RxCui { get; init; }
    public string? Instructions { get; init; }
    public string? Reason { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Status { get; init; }
    public string? DiscontinuedReason { get; init; }
    public int AddedByRole { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record CloudSleepSessionDto
{
    public Guid Id { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public double QualityScore { get; init; }
}

public sealed record CloudEnvironmentReadingDto
{
    public Guid Id { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string LocationDisplayName { get; init; } = string.Empty;
    public double PM25 { get; init; }
    public double PM10 { get; init; }
    public double O3 { get; init; }
    public double NO2 { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public int AirQuality { get; init; }
    public int AqiIndex { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed record CloudOcrDocumentDto
{
    public Guid Id { get; init; }
    public string OpaqueInternalName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public string SanitizedOcrPreview { get; init; } = string.Empty;
    public DateTime ScannedAt { get; init; }
}

public sealed record CloudMedicalHistoryEntryDto
{
    public Guid Id { get; init; }
    public Guid SourceDocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MedicationName { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public DateTime EventDate { get; init; }
}

// ── Medication sync DTOs ──────────────────────────────────────────────────

public sealed record MedicationSyncItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string? Frequency { get; init; }
    public int Route { get; init; }
    public string? RxCui { get; init; }
    public string? Instructions { get; init; }
    public string? Reason { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Status { get; init; }
    public string? DiscontinuedReason { get; init; }
    public int AddedByRole { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record MedicationSyncResponse
{
    public List<MedicationSyncItem>? Items { get; init; }
}

// ── Sleep sync DTOs ───────────────────────────────────────────────────────

public sealed record SleepSyncItem
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public double QualityScore { get; init; }
}

// ── Environment sync DTOs ─────────────────────────────────────────────────

public sealed record EnvironmentSyncItem
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string LocationDisplayName { get; init; } = string.Empty;
    public double PM25 { get; init; }
    public double PM10 { get; init; }
    public double O3 { get; init; }
    public double NO2 { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public int AirQuality { get; init; }
    public int AqiIndex { get; init; }
    public DateTime Timestamp { get; init; }
}

// ── OCR document sync DTOs ────────────────────────────────────────────────

public sealed record OcrDocumentSyncItem
{
    public Guid Id { get; init; }
    public string OpaqueInternalName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public string SanitizedOcrPreview { get; init; } = string.Empty;
    public DateTime ScannedAt { get; init; }
}

// ── Medical history sync DTOs ─────────────────────────────────────────────

public sealed record MedicalHistorySyncItem
{
    public Guid Id { get; init; }
    public Guid SourceDocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MedicationName { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public DateTime EventDate { get; init; }
}

