import Foundation

@MainActor
final class EcgMonitorViewModel: ObservableObject {
    @Published private(set) var hasProfile: Bool = false
    @Published private(set) var latestResult: EcgEvaluationResult?

    private let repository: EcgRepository
    private let evaluate: EvaluateEcgFrameUseCase

    init(repository: EcgRepository, evaluate: EvaluateEcgFrameUseCase) {
        self.repository = repository
        self.evaluate = evaluate
    }

    func load() async {
        hasProfile = await repository.hasPatientProfile()
    }

    func evaluateFrame(samples: [Double], spO2: Double, heartRate: Double) async {
        latestResult = await evaluate(samples: samples, spO2: spO2, heartRate: heartRate)
    }
}

