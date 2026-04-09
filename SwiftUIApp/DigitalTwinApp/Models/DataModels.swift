import Foundation

// MARK: - Medication Models

struct MedicationInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let name: String
    let dosage: String?
    let frequency: String?
    let route: Int      // MedicationRoute enum
    let rxCUI: String?
    let instructions: String?
    let reason: String?
    let prescribedBy: String?
    let startDate: Date
    let endDate: Date?
    let status: Int     // MedicationStatus enum
    let discontinuedReason: String?
    let addedByRole: Int // AddedByRole enum
    let isSynced: Bool

    var statusDisplay: String {
        switch status {
        case 0: return "Active"
        case 1: return "Inactive"
        case 2: return "Discontinued"
        default: return "Unknown"
        }
    }

    var isActive: Bool { status == 0 }
}

struct AddMedicationInput: Codable {
    let name: String
    let dosage: String?
    let frequency: String?
    let route: Int
    let rxCUI: String?
    let instructions: String?
    let reason: String?
    let prescribedBy: String?
}

struct DiscontinueMedicationInput: Codable {
    let medicationId: UUID
    let reason: String?
}

struct DrugSearchResult: Codable, Identifiable {
    let rxCUI: String
    let name: String
    let synonym: String?

    var id: String { rxCUI }
}

struct MedicationInteractionInfo: Codable, Identifiable {
    let drugA: String
    let drugB: String
    let severity: Int    // InteractionSeverity enum
    let description: String

    var id: String { "\(drugA)-\(drugB)" }

    var severityDisplay: String {
        switch severity {
        case 0: return "Low"
        case 1: return "Medium"
        case 2: return "High"
        default: return "Unknown"
        }
    }

    var severityColor: String {
        switch severity {
        case 0: return "green"
        case 1: return "orange"
        case 2: return "red"
        default: return "gray"
        }
    }
}

// MARK: - Environment Models

struct EnvironmentReadingInfo: Codable, Identifiable {
    var id: UUID
    let latitude: Double
    let longitude: Double
    let locationDisplayName: String?
    let pm25: Double?
    let pm10: Double?
    let o3: Double?
    let no2: Double?
    let temperature: Double?
    let humidity: Double?
    let airQualityLevel: Int  // AirQualityLevel enum
    let aqiIndex: Int?
    let timestamp: Date

    private enum CodingKeys: String, CodingKey {
        case id, latitude, longitude, locationDisplayName
        case pm25 = "pM25"
        case pm10 = "pM10"
        case o3
        case no2 = "nO2"
        case temperature, humidity
        case airQualityLevel = "airQuality"
        case aqiIndex, timestamp
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = (try? container.decode(UUID.self, forKey: .id)) ?? UUID()
        latitude = try container.decode(Double.self, forKey: .latitude)
        longitude = try container.decode(Double.self, forKey: .longitude)
        locationDisplayName = try container.decodeIfPresent(String.self, forKey: .locationDisplayName)
        pm25 = try container.decodeIfPresent(Double.self, forKey: .pm25)
        pm10 = try container.decodeIfPresent(Double.self, forKey: .pm10)
        o3 = try container.decodeIfPresent(Double.self, forKey: .o3)
        no2 = try container.decodeIfPresent(Double.self, forKey: .no2)
        temperature = try container.decodeIfPresent(Double.self, forKey: .temperature)
        humidity = try container.decodeIfPresent(Double.self, forKey: .humidity)
        airQualityLevel = try container.decode(Int.self, forKey: .airQualityLevel)
        aqiIndex = try container.decodeIfPresent(Int.self, forKey: .aqiIndex)
        timestamp = try container.decode(Date.self, forKey: .timestamp)
    }

    var airQualityDisplay: String {
        switch airQualityLevel {
        case 0: return "Good"
        case 1: return "Fair"
        case 2: return "Moderate"
        case 3: return "Poor"
        case 4: return "Very Poor"
        default: return "Unknown"
        }
    }

    var airQualityEmoji: String {
        switch airQualityLevel {
        case 0: return "🟢"
        case 1: return "🟡"
        case 2: return "🟠"
        case 3: return "🔴"
        case 4: return "🟣"
        default: return "⚪"
        }
    }
}

// MARK: - ECG Models

struct EcgFrameInput: Codable {
    let samples: [Double]
    let spO2: Double
    let heartRate: Double
    let timestamp: Date?
}

struct EcgEvaluationResult: Codable {
    let triageResult: Int   // TriageResult enum
    let alerts: [String]
    let heartRate: Double
    let spO2: Double

    var triageDisplay: String {
        switch triageResult {
        case 0: return "Normal"
        case 1: return "Warning"
        case 2: return "Critical"
        default: return "Unknown"
        }
    }

    var isCritical: Bool { triageResult == 2 }
    var isWarning: Bool { triageResult == 1 }
}

// MARK: - Chat Models

struct ChatMessageInfo: Codable, Identifiable {
    let id: UUID
    let content: String
    let isUser: Bool
    let timestamp: Date
}

struct CoachingAdviceInfo: Codable {
    let advice: String
    let timestamp: Date
}

// MARK: - Sleep Models

struct SleepSessionInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let startTime: Date
    let endTime: Date
    let durationMinutes: Int
    let qualityScore: Double?
    let isSynced: Bool

    var durationFormatted: String {
        let hours = durationMinutes / 60
        let mins = durationMinutes % 60
        if hours > 0 {
            return "\(hours)h \(mins)m"
        }
        return "\(mins)m"
    }

    var qualityDisplay: String {
        guard let score = qualityScore else { return "N/A" }
        if score >= 80 { return "Excellent" }
        if score >= 60 { return "Good" }
        if score >= 40 { return "Fair" }
        return "Poor"
    }
}

struct SleepSessionInput: Codable {
    let startTime: Date
    let endTime: Date
    let durationMinutes: Int
    let qualityScore: Double?
}

// MARK: - OCR & Medical History Models

struct OcrDocumentInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let opaqueInternalName: String?
    let mimeType: String?
    let pageCount: Int
    let sanitizedOcrPreview: String?
    let scannedAt: Date
    let isSynced: Bool

    var typeIcon: String {
        switch mimeType?.lowercased() {
        case "application/pdf": return "doc.fill"
        case let m where m?.contains("image") == true: return "photo.fill"
        default: return "doc.text.fill"
        }
    }
}

struct MedicalHistoryEntryInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let sourceDocumentId: UUID?
    let title: String?
    let medicationName: String?
    let dosage: String?
    let frequency: String?
    let duration: String?
    let confidence: Double?
    let notes: String?
    let summary: String?

    var displayTitle: String {
        title ?? medicationName ?? "Medical Entry"
    }
}

// MARK: - OCR Processing Models

struct DocumentIdentityInfo: Codable {
    let extractedName: String?
    let extractedCnp: String?
    let nameConfidence: Float
    let cnpConfidence: Float
}

struct IdentityValidationInfo: Codable {
    let isValid: Bool
    let nameMatched: Bool
    let cnpMatched: Bool
    let reason: String?
}

struct ExtractedMedicationFieldInfo: Codable {
    let name: String
    let dosage: String?
    let frequency: String?
    let rest: String?
}

struct HeuristicExtractionInfo: Codable {
    let patientName: String?
    let patientId: String?
    let reportDate: String?
    let doctorName: String?
    let diagnosis: String?
    let medications: [ExtractedMedicationFieldInfo]
}

struct ExtractedHistoryItemInfo: Codable {
    let title: String
    let medicationName: String
    let dosage: String
    let frequency: String
    let duration: String
    let notes: String
    let summary: String
    let confidence: Double
}

struct OcrProcessingResult: Codable {
    let documentType: String
    let identity: DocumentIdentityInfo?
    let validation: IdentityValidationInfo?
    let sanitizedText: String
    let extraction: HeuristicExtractionInfo?
    let historyItems: [ExtractedHistoryItemInfo]
}

struct SaveOcrDocumentInput: Codable {
    let opaqueInternalName: String
    let mimeType: String
    let pageCount: Int
    let pageTexts: [String]
}
