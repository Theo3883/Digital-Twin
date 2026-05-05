import Foundation

struct VitalSignInfo: Codable, Identifiable {
    let id: UUID
    let type: VitalSignType
    let value: Double
    let unit: String
    let source: String
    let timestamp: Date
    let isSynced: Bool
}

