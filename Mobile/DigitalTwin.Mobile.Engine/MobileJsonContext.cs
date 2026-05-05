using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Engine;

// Helper types for Engine JSON transport
public sealed record EcgFrameInput
{
    public double[] Samples { get; init; } = [];
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double SpO2 { get; init; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double HeartRate { get; init; }
    public DateTime Timestamp { get; init; }

    /// <summary>Number of leads in Samples (1 = Lead II only for rules, 12 = full 12-lead).</summary>
    public int? NumLeads { get; init; }

    /// <summary>XceptionTime (PTB-XL) probabilities keyed by label ("Normal", "AFib", "PVC", etc.).</summary>
    public Dictionary<string, double>? MlScores { get; init; }
}

public sealed record EcgEvaluationResult
{
    public EcgFrameDto? Frame { get; init; }
    public CriticalAlertDto? Alert { get; init; }
}

public sealed record CloudAuthStatusDto
{
    public bool IsAuthenticated { get; init; }
}

// Vault operation inputs/outputs for NativeBridge
public sealed record VaultInitInput
{
    public bool IsPasscodeSet { get; init; }
    public bool IsBiometryAvailable { get; init; }
    public string BiometryType { get; init; } = "None";
    public bool IsVaultInitialized { get; init; }
    public bool IsVaultUnlocked { get; init; }
    public string ActiveMode { get; init; } = "Strict";
}

public sealed record VaultStoreInput
{
    public string DocumentBase64 { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public string DocumentId { get; init; } = string.Empty;
}

public sealed record VaultResultDto
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? DocumentId { get; init; }
    public string? VaultPath { get; init; }
    public string? Sha256 { get; init; }
    public string? OpaqueInternalName { get; init; }
}

public sealed record ClassifyResultDto
{
    public string Type { get; init; } = "Unknown";
    public float Confidence { get; init; }
    public string Method { get; init; } = string.Empty;
}

public sealed record MlAuditSummaryDto
{
    public int TotalDocuments { get; init; }
    public double AverageOcrMs { get; init; }
    public double AverageClassifyMs { get; init; }
    public double AverageExtractMs { get; init; }
    public double AverageConfidence { get; init; }
    public double BertUsagePercent { get; init; }
}

// NativeAOT-safe System.Text.Json source generation context.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
// Auth & Profile
[JsonSerializable(typeof(AuthenticationResult))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserUpdateInput))]
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
[JsonSerializable(typeof(CloudAuthStatusDto))]
[JsonSerializable(typeof(Dictionary<string, double>))]
// Chat
[JsonSerializable(typeof(ChatMessageDto))]
[JsonSerializable(typeof(ChatMessageDto[]), TypeInfoPropertyName = "ChatMessageDtoArray")]
// Coaching
[JsonSerializable(typeof(CoachingAdviceDto))]
[JsonSerializable(typeof(CoachingSectionDto))]
[JsonSerializable(typeof(CoachingSectionDto[]), TypeInfoPropertyName = "CoachingSectionDtoArray")]
[JsonSerializable(typeof(CoachingActionDto))]
[JsonSerializable(typeof(CoachingActionDto[]), TypeInfoPropertyName = "CoachingActionDtoArray")]
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
[JsonSerializable(typeof(BuildStructuredDocumentInput))]
// Advanced OCR / Vault / ML
[JsonSerializable(typeof(VaultInitInput))]
[JsonSerializable(typeof(VaultStoreInput))]
[JsonSerializable(typeof(VaultResultDto))]
[JsonSerializable(typeof(ClassifyResultDto))]
[JsonSerializable(typeof(MlAuditSummaryDto))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.EncryptedDocumentDescriptor))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.StructuredMedicalDocument))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ExtractedField<string>))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ExtractedLabResult))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ExtractedLabResult[]), TypeInfoPropertyName = "ExtractedLabResultArray")]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ExtractedMedication))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ExtractedMedication[]), TypeInfoPropertyName = "ExtractedMedicationArray")]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ReviewFlag))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.ReviewFlag[]), TypeInfoPropertyName = "ReviewFlagArray")]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.Structured.DocumentExtractionMetrics))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.ML.ClassificationResult))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.ML.MlAuditRecord))]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.ML.MlAuditRecord[]), TypeInfoPropertyName = "MlAuditRecordArray")]
[JsonSerializable(typeof(DigitalTwin.Mobile.OCR.Models.ML.MlPerformanceSummary))]
// Utility
[JsonSerializable(typeof(string[]), TypeInfoPropertyName = "StringArray")]
[JsonSerializable(typeof(NativeBridge.OperationResultDto))]
[JsonSerializable(typeof(NativeBridge.RecordVitalSignsResultDto))]
// Doctor Assignments
[JsonSerializable(typeof(AssignedDoctorDto))]
[JsonSerializable(typeof(AssignedDoctorDto[]), TypeInfoPropertyName = "AssignedDoctorDtoArray")]
// Environment Analytics
[JsonSerializable(typeof(EnvironmentAnalyticsDto))]
[JsonSerializable(typeof(HourlyDataPoint))]
[JsonSerializable(typeof(HourlyDataPoint[]), TypeInfoPropertyName = "HourlyDataPointArray")]
// Notifications
[JsonSerializable(typeof(NotificationItem))]
[JsonSerializable(typeof(NotificationItem[]), TypeInfoPropertyName = "NotificationItemArray")]
[JsonSerializable(typeof(DigitalTwin.Mobile.Application.DTOs.NotificationItemDto))]
[JsonSerializable(typeof(DigitalTwin.Mobile.Application.DTOs.NotificationItemDto[]), TypeInfoPropertyName = "NotificationItemDtoArray")]
public partial class MobileJsonContext : JsonSerializerContext;
