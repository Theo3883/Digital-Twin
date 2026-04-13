using System.Text.Json.Serialization;

namespace DigitalTwin.Mobile.Infrastructure.Services;

// NativeAOT-safe System.Text.Json source generation context for HTTP payloads.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
// Cloud sync types
[JsonSerializable(typeof(GoogleAuthRequest))]
[JsonSerializable(typeof(DeviceRequestEnvelope<UpsertUserRequest>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<UpsertPatientRequest>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<VitalAppendRequestItem>))]
[JsonSerializable(typeof(VitalAppendRequestItem))]
[JsonSerializable(typeof(List<VitalAppendRequestItem>))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(MobileBootstrapDto))]
[JsonSerializable(typeof(CloudMedicationDto))]
[JsonSerializable(typeof(List<CloudMedicationDto>))]
[JsonSerializable(typeof(CloudSleepSessionDto))]
[JsonSerializable(typeof(List<CloudSleepSessionDto>))]
[JsonSerializable(typeof(CloudEnvironmentReadingDto))]
[JsonSerializable(typeof(List<CloudEnvironmentReadingDto>))]
[JsonSerializable(typeof(CloudOcrDocumentDto))]
[JsonSerializable(typeof(List<CloudOcrDocumentDto>))]
[JsonSerializable(typeof(CloudMedicalHistoryEntryDto))]
[JsonSerializable(typeof(List<CloudMedicalHistoryEntryDto>))]
[JsonSerializable(typeof(UserProfileResponse))]
[JsonSerializable(typeof(CloudUserDto))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(PatientProfileResponse))]
[JsonSerializable(typeof(CloudPatientDto))]
[JsonSerializable(typeof(VitalSyncResponse))]
[JsonSerializable(typeof(VitalSignsResponse))]
[JsonSerializable(typeof(CloudVitalSignDto))]
[JsonSerializable(typeof(List<CloudVitalSignDto>))]
// Medication sync
[JsonSerializable(typeof(MedicationSyncItem))]
[JsonSerializable(typeof(List<MedicationSyncItem>))]
[JsonSerializable(typeof(MedicationSyncResponse))]
[JsonSerializable(typeof(DeviceRequestEnvelope<MedicationSyncItem>))]
// Sleep sync
[JsonSerializable(typeof(SleepSyncItem))]
[JsonSerializable(typeof(List<SleepSyncItem>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<SleepSyncItem>))]
// Environment sync
[JsonSerializable(typeof(EnvironmentSyncItem))]
[JsonSerializable(typeof(List<EnvironmentSyncItem>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<EnvironmentSyncItem>))]
// OCR document sync
[JsonSerializable(typeof(OcrDocumentSyncItem))]
[JsonSerializable(typeof(List<OcrDocumentSyncItem>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<OcrDocumentSyncItem>))]
// Medical history sync
[JsonSerializable(typeof(MedicalHistorySyncItem))]
[JsonSerializable(typeof(List<MedicalHistorySyncItem>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<MedicalHistorySyncItem>))]
// Doctor assignments (read-only from cloud)
[JsonSerializable(typeof(AssignedDoctorsResponse))]
[JsonSerializable(typeof(CloudAssignedDoctorDto))]
[JsonSerializable(typeof(List<CloudAssignedDoctorDto>))]
public partial class InfrastructureJsonContext : JsonSerializerContext;

// NativeAOT-safe JSON context for third-party integration API payloads.
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
// RxNav
[JsonSerializable(typeof(RxNavDrugsResponse))]
[JsonSerializable(typeof(RxNavPropertiesResponse))]
[JsonSerializable(typeof(RxNavRelatedResponse))]
// openFDA
[JsonSerializable(typeof(OpenFdaResponse))]
// Gemini
[JsonSerializable(typeof(GeminiRequest))]
[JsonSerializable(typeof(GeminiResponse))]
[JsonSerializable(typeof(OpenRouterChatRequest))]
[JsonSerializable(typeof(OpenRouterResponseFormat))]
[JsonSerializable(typeof(OpenRouterChatResponse))]
// OpenWeather
[JsonSerializable(typeof(OpenWeatherResponse))]
[JsonSerializable(typeof(OpenWeatherAqResponse))]
public partial class IntegrationJsonContext : JsonSerializerContext;

