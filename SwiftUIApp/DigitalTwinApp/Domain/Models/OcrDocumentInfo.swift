import Foundation

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

