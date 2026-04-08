namespace DigitalTwin.Application.DTOs.MobileSync;

/// <summary>
/// Patient data for mobile sync operations.
/// </summary>
public record PatientSyncDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public decimal? Weight { get; init; }
    public decimal? Height { get; init; }
    public int? BloodPressureSystolic { get; init; }
    public int? BloodPressureDiastolic { get; init; }
    public decimal? Cholesterol { get; init; }
    public string? Cnp { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// Request to upsert patient data to cloud.
/// </summary>
public record UpsertPatientRequest : MobileSyncRequestBase
{
    public PatientSyncDto Patient { get; init; } = new();
}

/// <summary>
/// Response from patient upsert operation.
/// </summary>
public record UpsertPatientResponse : MobileSyncResponseBase
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CloudPatientId { get; init; }
}

/// <summary>
/// Response containing patient profile data from cloud.
/// </summary>
public record GetPatientProfileResponse : MobileSyncResponseBase
{
    public PatientSyncDto? Patient { get; init; }
}