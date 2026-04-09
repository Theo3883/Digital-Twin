import Foundation

protocol EcgRepository: Sendable {
    func hasPatientProfile() async -> Bool
    func evaluateFrame(samples: [Double], spO2: Double, heartRate: Double) async -> EcgEvaluationResult?
}

