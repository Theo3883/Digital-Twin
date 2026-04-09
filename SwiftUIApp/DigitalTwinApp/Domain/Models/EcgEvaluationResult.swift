import Foundation

struct EcgEvaluationResult: Codable {
    let triageResult: Int   // TriageResult enum
    let alerts: [String]
    let heartRate: Double
    let spO2: Double

    var triageDisplay: String {
        switch triageResult {
        case 0: return "Normal"
        case 1: return "Warning"
        case 2: return "Critical"
        default: return "Unknown"
        }
    }

    var isCritical: Bool { triageResult == 2 }
    var isWarning: Bool { triageResult == 1 }
}

