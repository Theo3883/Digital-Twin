import Foundation

enum VitalSignType: Int, Codable, CaseIterable {
    case heartRate = 0
    case bloodPressure = 1
    case temperature = 2
    case oxygenSaturation = 3
    case respiratoryRate = 4
    case bloodGlucose = 5
    case weight = 6
    case height = 7
    case bmi = 8
    case stepCount = 9
    case caloriesBurned = 10
    case sleepDuration = 11

    var displayName: String {
        switch self {
        case .heartRate: return "Heart Rate"
        case .bloodPressure: return "Blood Pressure"
        case .temperature: return "Temperature"
        case .oxygenSaturation: return "Oxygen Saturation"
        case .respiratoryRate: return "Respiratory Rate"
        case .bloodGlucose: return "Blood Glucose"
        case .weight: return "Weight"
        case .height: return "Height"
        case .bmi: return "BMI"
        case .stepCount: return "Step Count"
        case .caloriesBurned: return "Calories Burned"
        case .sleepDuration: return "Sleep Duration"
        }
    }

    var unit: String {
        switch self {
        case .heartRate: return "bpm"
        case .bloodPressure: return "mmHg"
        case .temperature: return "°F"
        case .oxygenSaturation: return "%"
        case .respiratoryRate: return "breaths/min"
        case .bloodGlucose: return "mg/dL"
        case .weight: return "lbs"
        case .height: return "in"
        case .bmi: return ""
        case .stepCount: return "steps"
        case .caloriesBurned: return "cal"
        case .sleepDuration: return "hours"
        }
    }
}

