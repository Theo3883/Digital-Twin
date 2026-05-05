import Foundation

struct MedicalHistoryEntryInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let sourceDocumentId: UUID?
    let title: String
    let medicationName: String
    let dosage: String
    let frequency: String
    let duration: String
    let confidence: Double
    let notes: String
    let summary: String
    let eventDate: Date
    let createdAt: Date?
    let updatedAt: Date?

    var displayTitle: String {
        title.isEmpty ? (medicationName.isEmpty ? "Medical Entry" : medicationName) : title
    }
}

