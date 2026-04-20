import Foundation

protocol EcgRepository: Sendable {
    func hasPatientProfile() async -> Bool
    func evaluateFrame(samples: [Double], spO2: Double, heartRate: Double,
                       mlScores: [String: Double]?) async -> EcgEvaluationResult?
}
