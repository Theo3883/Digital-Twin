using System.Text.Json.Serialization;

namespace DigitalTwin.Mobile.Infrastructure.Services;

// NativeAOT-safe System.Text.Json source generation context for HTTP payloads.
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(GoogleAuthRequest))]
[JsonSerializable(typeof(DeviceRequestEnvelope<UpsertUserRequest>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<UpsertPatientRequest>))]
[JsonSerializable(typeof(DeviceRequestEnvelope<VitalAppendRequestItem>))]
[JsonSerializable(typeof(VitalAppendRequestItem))]
[JsonSerializable(typeof(List<VitalAppendRequestItem>))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(UserProfileResponse))]
[JsonSerializable(typeof(CloudUserDto))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(PatientProfileResponse))]
[JsonSerializable(typeof(CloudPatientDto))]
[JsonSerializable(typeof(VitalSyncResponse))]
[JsonSerializable(typeof(VitalSignsResponse))]
[JsonSerializable(typeof(CloudVitalSignDto))]
[JsonSerializable(typeof(List<CloudVitalSignDto>))]
public partial class InfrastructureJsonContext : JsonSerializerContext;

