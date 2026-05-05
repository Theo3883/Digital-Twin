import Foundation

@MainActor
final class EcgMonitorViewModel: ObservableObject {

    // MARK: - Published State

    @Published private(set) var hasProfile: Bool = false
    @Published private(set) var latestResult: EcgEvaluationResult?
    @Published private(set) var mlClassification: ECGClassification?
    @Published private(set) var isTriageActive: Bool = false
    @Published private(set) var frameCount: Int = 0
    @Published private(set) var mlLoadError: String?

    // MARK: - Dependencies

    private let repository: EcgRepository
    private let evaluate: EvaluateEcgFrameUseCase
    let classifier: ECGClassifierProtocol
    private let triageNotifications = TriageNotificationService.shared

    // MARK: - Init

    init(repository: EcgRepository,
         evaluate: EvaluateEcgFrameUseCase,
         classifier: ECGClassifierProtocol = ECGClassifierService.shared) {
        self.repository = repository
        self.evaluate = evaluate
        self.classifier = classifier

        Task { await self.loadClassifier() }
    }

    /// Loads the ONNX model on a background thread to avoid blocking the UI.
    nonisolated private func loadClassifier() async {
        do {
            try await Task.sleep(nanoseconds: 100_000_000)
            try classifier.load()
        } catch {
            let msg = error.localizedDescription
            print("[EcgMonitorViewModel] ONNX load failed: \(msg)")
            await MainActor.run { self.mlLoadError = msg }
        }
    }

    func load() async {
        hasProfile = await repository.hasPatientProfile()
    }

    // MARK: - Triage Evaluation (called every ~1 second)

    /// Runs one evaluation tick: domain-rule triage + ML classification.
    ///
    /// - Parameter ble: The live `BLEManager` that provides the 12-lead ring buffer.
    func evaluateFrame(ble: BLEManager) async {

        // 1. Extract Lead-II history and current vitals for domain-rule evaluation.
        let leadII = ble.leadIIBuffer.suffix(4096).map { Double($0) }
        let spO2 = Double(ble.spO2)
        let hr = Double(ble.heartRate)

        // 2. Feed the full ecgBuffer [12][up to 1000] directly to the classifier.
        //    The classifier handles ADC→mV conversion and checks if 1000 samples
        //    are available before running inference.
        var mlScores: [String: Double]? = nil

        if classifier.isLoaded {
            let classification = classifier.classify(ecgBuffer: ble.ecgBuffer, heartRate: hr)
            if let classification {
                mlClassification = classification
                mlScores = classification.probabilities
            }
        }

        // 3. Run the domain-rule engine (HR/SpO₂ thresholds, rhythm checks, etc.).
        var result = await evaluate(
            samples: Array(leadII),
            spO2: spO2,
            heartRate: hr,
            mlScores: mlScores
        )

        // 4. Merge the ML output into the triage result so the UI can display both.
        if var r = result {
            if let ml = mlClassification {
                r.mlTopLabel = ml.topLabel
                r.mlConfidence = ml.topConfidence
                r.mlProbabilities = ml.probabilities
            }
            r.isConnected = true
            result = r
        }

        latestResult = result
        isTriageActive = true
        frameCount += 1

        await triageNotifications.evaluateAndNotify(result: result)
    }

    // MARK: - Connection State

    /// Immediately reflects an ESP32 disconnect in the triage panel.
    func disconnectTriage() {
        isTriageActive = false
        latestResult = .disconnected
        mlClassification = nil
        frameCount = 0
        print("[EcgMonitorViewModel] Triage suspended — ESP32 disconnected")
    }

    /// Clears stale state so the next evaluation starts fresh after reconnect.
    func reconnectTriage() {
        latestResult = nil
        mlClassification = nil
        isTriageActive = false
        frameCount = 0
        print("[EcgMonitorViewModel] Triage resumed — ESP32 connected")
    }
}