using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Application.DTOs;

namespace DigitalTwin.Mobile.Engine;

// NativeAOT-safe System.Text.Json source generation context.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
[JsonSerializable(typeof(AuthenticationResult))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(PatientDto))]
[JsonSerializable(typeof(PatientUpdateInput))]
[JsonSerializable(typeof(VitalSignDto))]
[JsonSerializable(typeof(VitalSignDto[]), TypeInfoPropertyName = "VitalSignDtoArray")]
[JsonSerializable(typeof(VitalSignInput))]
[JsonSerializable(typeof(VitalSignInput[]), TypeInfoPropertyName = "VitalSignInputArray")]
[JsonSerializable(typeof(NativeBridge.OperationResultDto))]
[JsonSerializable(typeof(NativeBridge.RecordVitalSignsResultDto))]
public partial class MobileJsonContext : JsonSerializerContext;

