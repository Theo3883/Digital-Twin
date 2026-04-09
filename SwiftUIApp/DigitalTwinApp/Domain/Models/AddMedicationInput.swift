import Foundation

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

