import Foundation

struct MedicationInteractionInfo: Codable, Identifiable {
    let drugA: String
    let drugB: String
    let severity: Int    // InteractionSeverity enum
    let description: String

    var id: String { "\(drugA)-\(drugB)" }

    var severityDisplay: String {
        switch severity {
        case 0: return "Low"
        case 1: return "Medium"
        case 2: return "High"
        default: return "Unknown"
        }
    }

    var severityColor: String {
        switch severity {
        case 0: return "green"
        case 1: return "orange"
        case 2: return "red"
        default: return "gray"
        }
    }
}

