import Foundation

struct VitalSignInput: Codable {
    let type: VitalSignType
    let value: Double
    let unit: String
    let source: String
    let timestamp: Date?
}

