import Foundation

@MainActor
final class EcgMonitorViewModel: ObservableObject {
    @Published private(set) var hasProfile: Bool = false
    @Published private(set) var latestResult: EcgEvaluationResult?
    @Published private(set) var mlClassification: ECGClassification?
    @Published private(set) var isTriageActive: Bool = false
    @Published private(set) var frameCount: Int = 0
    @Published private(set) var mlLoadError: String?

    private let repository: EcgRepository
    private let evaluate: EvaluateEcgFrameUseCase
    private let classifier: PTBXLClassifierService

    init(repository: EcgRepository, evaluate: EvaluateEcgFrameUseCase,
         classifier: PTBXLClassifierService = PTBXLClassifierService()) {
        self.repository = repository
        self.evaluate = evaluate
        self.classifier = classifier

        // Load the CoreML model in the background
        Task { [weak self] in
            guard let self else { return }
            do {
                try self.classifier.load()
            } catch {
                let msg = error.localizedDescription
                print("[EcgMonitorViewModel] CoreML load failed: \(msg)")
                await MainActor.run {
                    self.mlLoadError = msg
                }
            }
        }
    }

    func load() async {
        hasProfile = await repository.hasPatientProfile()
    }

    // MARK: - Triage Evaluation (called on 1-second timer)

    /// Called each evaluation tick.
    /// - Parameters:
    ///   - ble: the live BLEManager providing the 12-lead ring buffer
    func evaluateFrame(ble: BLEManager) async {
        // 1. Use Lead II (index 1) as the 1D sample array for domain rules
        let leadII = ble.leadIIBuffer.suffix(4096).map { Double($0) }
        let spO2   = Double(ble.spO2)
        let hr     = Double(ble.heartRate)

        // 2. Run CoreML on the full 12-lead buffer when ready (≥ 4096 samples/lead)
        var mlScores: [String: Double]? = nil
        if ble.isBufferReady, classifier.isLoaded {
            let mlInput = ble.getMLInput()
            let classification = classifier.classify(mlInput: mlInput)
            mlClassification = classification
            mlScores = classification?.probabilities
        }

        // 3. Call engine (domain rules + ML-enhanced triage)
        var result = await evaluate(samples: Array(leadII), spO2: spO2, heartRate: hr,
                                    mlScores: mlScores)

        // 4. Merge CoreML output into the result
        if var r = result {
            if let ml = mlClassification {
                r.mlTopLabel      = ml.topLabel
                r.mlConfidence    = ml.topConfidence
                r.mlProbabilities = ml.probabilities
            }
            r.isConnected = true
            result = r
        }

        latestResult = result
        isTriageActive = true
        frameCount += 1
    }

    // MARK: - Connection State

    /// Called when ESP32 disconnects — immediately reflects in the triage panel
    func disconnectTriage() {
        isTriageActive = false
        latestResult   = .disconnected
        mlClassification = nil
        frameCount = 0
        print("[EcgMonitorViewModel] Triage suspended — ESP32 disconnected")
    }

    /// Called when ESP32 reconnects — clears stale state, ready for next evaluation
    func reconnectTriage() {
        latestResult     = nil
        mlClassification = nil
        isTriageActive   = false
        frameCount       = 0
        print("[EcgMonitorViewModel] Triage resumed — ESP32 connected")
    }
}
