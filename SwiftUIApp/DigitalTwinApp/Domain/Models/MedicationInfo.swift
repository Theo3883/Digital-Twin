import Foundation

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

