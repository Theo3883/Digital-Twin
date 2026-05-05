import Foundation

enum VitalSignType: Int, Codable, CaseIterable {
    case heartRate = 0
    case spO2 = 1
    case steps = 2
    case calories = 3
    case activeEnergy = 4
    case exerciseMinutes = 5
    case standHours = 6

    var displayName: String {
        switch self {
        case .heartRate: return "Heart Rate"
        case .spO2: return "SpO2"
        case .steps: return "Steps"
        case .calories: return "Calories"
        case .activeEnergy: return "Active Energy"
        case .exerciseMinutes: return "Exercise Minutes"
        case .standHours: return "Stand Hours"
        }
    }

    var unit: String {
        switch self {
        case .heartRate: return "bpm"
        case .spO2: return "%"
        case .steps: return "steps"
        case .calories: return "kcal"
        case .activeEnergy: return "kcal"
        case .exerciseMinutes: return "min"
        case .standHours: return "hrs"
        }
    }
}

