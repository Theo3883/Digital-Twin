import Foundation

struct AddMedicationInput: Codable {
    let name: String
    let dosage: String
    let frequency: String?
    let route: Int
    let rxCui: String?
    let instructions: String?
    let reason: String?
    let prescribedByUserId: UUID?
    let startDate: Date?
    let addedByRole: Int
}

