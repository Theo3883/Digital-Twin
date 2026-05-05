import Foundation

struct EvaluateEcgFrameUseCase: Sendable {
    private let repository: EcgRepository

    init(repository: EcgRepository) {
        self.repository = repository
    }

    func callAsFunction(samples: [Double], spO2: Double, heartRate: Double,
                        mlScores: [String: Double]? = nil) async -> EcgEvaluationResult? {
        await repository.evaluateFrame(samples: samples, spO2: spO2, heartRate: heartRate,
                                       mlScores: mlScores)
    }
}


