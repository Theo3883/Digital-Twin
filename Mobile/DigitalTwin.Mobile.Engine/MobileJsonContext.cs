using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Application.DTOs;

namespace DigitalTwin.Mobile.Engine;

// Helper types for Engine JSON transport
public sealed record EcgFrameInput
{
    public double[] Samples { get; init; } = [];
    public double SpO2 { get; init; }
    public int HeartRate { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed record EcgEvaluationResult
{
    public EcgFrameDto? Frame { get; init; }
    public CriticalAlertDto? Alert { get; init; }
}

public sealed record SaveOcrDocumentInput
{
    public string OpaqueInternalName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public string[] PageTexts { get; init; } = [];
}

// NativeAOT-safe System.Text.Json source generation context.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
// Auth & Profile
[JsonSerializable(typeof(AuthenticationResult))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(PatientDto))]
[JsonSerializable(typeof(PatientUpdateInput))]
// Vital Signs
[JsonSerializable(typeof(VitalSignDto))]
[JsonSerializable(typeof(VitalSignDto[]), TypeInfoPropertyName = "VitalSignDtoArray")]
[JsonSerializable(typeof(VitalSignInput))]
[JsonSerializable(typeof(VitalSignInput[]), TypeInfoPropertyName = "VitalSignInputArray")]
// Medications
[JsonSerializable(typeof(MedicationDto))]
[JsonSerializable(typeof(MedicationDto[]), TypeInfoPropertyName = "MedicationDtoArray")]
[JsonSerializable(typeof(AddMedicationInput))]
[JsonSerializable(typeof(DiscontinueMedicationInput))]
[JsonSerializable(typeof(DrugSearchResultDto))]
[JsonSerializable(typeof(DrugSearchResultDto[]), TypeInfoPropertyName = "DrugSearchResultDtoArray")]
[JsonSerializable(typeof(MedicationInteractionDto))]
[JsonSerializable(typeof(MedicationInteractionDto[]), TypeInfoPropertyName = "MedicationInteractionDtoArray")]
// Environment
[JsonSerializable(typeof(EnvironmentReadingDto))]
// ECG
[JsonSerializable(typeof(EcgFrameInput))]
[JsonSerializable(typeof(EcgFrameDto))]
[JsonSerializable(typeof(CriticalAlertDto))]
[JsonSerializable(typeof(EcgEvaluationResult))]
// Chat
[JsonSerializable(typeof(ChatMessageDto))]
[JsonSerializable(typeof(ChatMessageDto[]), TypeInfoPropertyName = "ChatMessageDtoArray")]
// Coaching
[JsonSerializable(typeof(CoachingAdviceDto))]
// Sleep
[JsonSerializable(typeof(SleepSessionInput))]
[JsonSerializable(typeof(SleepSessionDto))]
[JsonSerializable(typeof(SleepSessionDto[]), TypeInfoPropertyName = "SleepSessionDtoArray")]
// OCR / Medical History
[JsonSerializable(typeof(OcrDocumentDto))]
[JsonSerializable(typeof(OcrDocumentDto[]), TypeInfoPropertyName = "OcrDocumentDtoArray")]
[JsonSerializable(typeof(MedicalHistoryEntryDto))]
[JsonSerializable(typeof(MedicalHistoryEntryDto[]), TypeInfoPropertyName = "MedicalHistoryEntryDtoArray")]
// OCR Text Processing
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.DocumentIdentity))]
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.IdentityValidationResult))]
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.HeuristicExtractionResult))]
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.OcrTextProcessingResult))]
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.ExtractedHistoryItem))]
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.ExtractedHistoryItem[]), TypeInfoPropertyName = "ExtractedHistoryItemArray")]
[JsonSerializable(typeof(DigitalTwin.Mobile.Domain.Models.ExtractedMedicationField))]
[JsonSerializable(typeof(SaveOcrDocumentInput))]
// Utility
[JsonSerializable(typeof(string[]), TypeInfoPropertyName = "StringArray")]
[JsonSerializable(typeof(NativeBridge.OperationResultDto))]
[JsonSerializable(typeof(NativeBridge.RecordVitalSignsResultDto))]
public partial class MobileJsonContext : JsonSerializerContext;
