import Foundation

struct DiscontinueMedicationInput: Codable {
    let medicationId: UUID
    let reason: String?
}

