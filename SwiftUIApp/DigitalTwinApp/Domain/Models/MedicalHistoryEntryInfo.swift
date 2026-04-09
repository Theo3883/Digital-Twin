import Foundation

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

