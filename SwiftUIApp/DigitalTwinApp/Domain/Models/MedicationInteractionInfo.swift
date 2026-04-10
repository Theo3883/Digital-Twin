import Foundation

struct MedicationInteractionInfo: Codable, Identifiable {
    let drugARxCui: String
    let drugBRxCui: String
    let severity: Int    // InteractionSeverity enum: None=0, Low=1, Medium=2, High=3
    let description: String

    var id: String { "\(drugARxCui)-\(drugBRxCui)" }

    var severityDisplay: String {
        switch severity {
        case 0: return "None"
        case 1: return "Low"
        case 2: return "Medium"
        case 3: return "High"
        default: return "Unknown"
        }
    }

    var severityColor: String {
        switch severity {
        case 0: return "gray"
        case 1: return "green"
        case 2: return "orange"
        case 3: return "red"
        default: return "gray"
        }
    }
}

