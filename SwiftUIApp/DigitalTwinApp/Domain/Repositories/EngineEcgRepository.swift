import Foundation

@MainActor
final class EngineEcgRepository: EcgRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func hasPatientProfile() async -> Bool {
        engine.patientProfile != nil
    }

    func evaluateFrame(samples: [Double], spO2: Double, heartRate: Double) async -> EcgEvaluationResult? {
        await engine.evaluateEcgFrame(samples: samples, spO2: spO2, heartRate: heartRate)
    }
}

