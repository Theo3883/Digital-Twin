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
}

public sealed record UserProfileResponse
{
    public CloudUserDto? User { get; init; }
}

public sealed record CloudUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Phone { get; init; }
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
    public int Type { get; init; }
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

