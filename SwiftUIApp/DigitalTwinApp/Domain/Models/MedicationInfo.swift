import Foundation

struct MedicationInfo: Codable, Identifiable {
    let id: UUID
    let name: String
    let dosage: String
    let frequency: String?
    let route: Int      // MedicationRoute enum
    let rxCui: String?
    let instructions: String?
    let reason: String?
    let prescribedByUserId: UUID?
    let startDate: Date?
    let endDate: Date?
    let status: Int     // MedicationStatus enum
    let discontinuedReason: String?
    let addedByRole: Int // AddedByRole enum
    let createdAt: Date?
    let isSynced: Bool

    enum CodingKeys: String, CodingKey {
        case id
        case name
        case dosage
        case frequency
        case route
        case status
        case rxCui
        case instructions
        case reason
        case prescribedByUserId
        case startDate
        case endDate
        case discontinuedReason
        case addedByRole
        case createdAt
        case isSynced
    }

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

