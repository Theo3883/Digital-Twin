import Foundation

struct OcrDocumentInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let opaqueInternalName: String
    let mimeType: String
    let documentType: String
    let pageCount: Int
    let sha256OfNormalized: String
    let encryptedVaultPath: String
    let sanitizedOcrPreview: String
    let scannedAt: Date
    let createdAt: Date?
    let updatedAt: Date?
    let isDirty: Bool
    let syncedAt: Date?

    var isSynced: Bool { !isDirty && syncedAt != nil }

    var shortDocRef: String {
        let hex = id.uuidString.replacingOccurrences(of: "-", with: "")
        return String(hex.prefix(8)).uppercased()
    }

    var shortContentHash: String {
        guard sha256OfNormalized.count >= 16 else { return "-" }
        return String(sha256OfNormalized.prefix(16))
    }

    var typeIcon: String {
        switch mimeType.lowercased() {
        case "application/pdf": return "doc.fill"
        case let m where m.contains("image"): return "photo.fill"
        default: return "doc.text.fill"
        }
    }

    var displayType: String {
        switch documentType {
        case "LabResult": return "Lab Result"
        case "Prescription": return "Prescription"
        case "Referral": return "Referral"
        case "Discharge": return "Discharge"
        case "MedicalCertificate": return "Medical Certificate"
        case "ImagingReport": return "Imaging Report"
        case "EcgReport": return "ECG Report"
        case "OperativeReport": return "Operative Report"
        case "ConsultationNote": return "Consultation Note"
        case "Unknown": return "Document"
        default: return documentType.isEmpty ? "Document" : documentType
        }
    }
}
