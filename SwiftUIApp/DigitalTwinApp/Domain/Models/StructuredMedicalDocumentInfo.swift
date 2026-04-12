import Foundation

// MARK: - Enums

enum ExtractionMethodInfo: String, Codable {
    case heuristicRegex = "HeuristicRegex"
    case mlBertTokenClassifier = "MlBertTokenClassifier"
    case mlNlClassifier = "MlNlClassifier"
    case boundingBoxAlignment = "BoundingBoxAlignment"
    case combined = "Combined"
}

enum ReviewSeverityInfo: String, Codable {
    case info = "Info"
    case warning = "Warning"
    case critical = "Critical"
}

// MARK: - Extracted Field

struct ExtractedFieldInfo: Codable {
    let value: String
    let confidence: Float
    let method: ExtractionMethodInfo

    var needsReview: Bool { confidence < 0.70 }
}

// MARK: - Review Flag

struct ReviewFlagInfo: Codable, Identifiable {
    let fieldName: String
    let reason: String
    let severity: ReviewSeverityInfo

    var id: String { "\(fieldName)-\(reason)" }
}

// MARK: - Metrics

struct DocumentExtractionMetricsInfo: Codable {
    let totalTokens: Int
    let averageFieldConfidence: Float
    let ocrDuration: String
    let classificationDuration: String
    let extractionDuration: String
}

// MARK: - Extraction Results

struct ExtractedMedicationInfo: Codable, Identifiable {
    let name: ExtractedFieldInfo
    let dose: ExtractedFieldInfo?
    let frequency: ExtractedFieldInfo?
    let route: ExtractedFieldInfo?
    let duration: ExtractedFieldInfo?

    var id: String { name.value }
}

struct ExtractedLabResultInfo: Codable, Identifiable {
    let analysisName: ExtractedFieldInfo
    let value: ExtractedFieldInfo
    let unit: ExtractedFieldInfo?
    let referenceRange: ExtractedFieldInfo?
    let sampleDate: ExtractedFieldInfo?
    let isOutOfRange: Bool

    var id: String { "\(analysisName.value)-\(value.value)" }
}

// MARK: - Structured Medical Document

struct StructuredMedicalDocumentInfo: Codable, Identifiable {
    let documentId: UUID
    let documentType: String
    let classificationConfidence: Float
    let classificationMethod: String
    let primaryExtractionMethod: ExtractionMethodInfo

    let patientName: ExtractedFieldInfo?
    let patientId: ExtractedFieldInfo?
    let reportDate: ExtractedFieldInfo?
    let doctorName: ExtractedFieldInfo?
    let diagnosis: ExtractedFieldInfo?

    let medications: [ExtractedMedicationInfo]
    let labResults: [ExtractedLabResultInfo]

    let extractedAt: Date
    let reviewFlags: [ReviewFlagInfo]
    let metrics: DocumentExtractionMetricsInfo?

    var id: UUID { documentId }
    var requiresReview: Bool { reviewFlags.contains { $0.severity == .critical } }
}
