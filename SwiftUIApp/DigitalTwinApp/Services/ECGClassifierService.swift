import Foundation
import OnnxRuntimeBindings

protocol ECGClassifierProtocol: AnyObject, Sendable {
    var isLoaded: Bool { get }
    func load() throws
    func classify(ecgBuffer: [[Int]], heartRate: Double) -> ECGClassification?
}

struct ECGClassification {
    let probabilities: [String: Double]
    let topLabel: String
    let topConfidence: Double
    let isAbnormal: Bool

    var summary: String {
        "\(topLabel) (\(Int(topConfidence * 100))%)"
    }
}

// MARK: - Classifier

final class ECGClassifierService: ECGClassifierProtocol, @unchecked Sendable {

    /// Singleton — one model + one buffer across all views.
    static let shared = ECGClassifierService()

    private var ortEnv: ORTEnv?
    private var ortSession: ORTSession?
    private(set) var isLoaded = false

    let labels = ["AFib", "Bradycardia", "LongQT", "Normal", "PVC", "STEMI", "Tachycardia"]
     
    private static let adcScale: Float = 800.0
    private static let samplesNeeded = 1000
    private static let numLeads = 12

    private init() {}

    func load() throws {
        guard !isLoaded else { return }
        ortEnv = try ORTEnv(loggingLevel: .warning)

        guard let modelPath = Bundle.main.path(forResource: "ECGModel", ofType: "onnx") else {
            throw NSError(domain: "ECG", code: 1,
                          userInfo: [NSLocalizedDescriptionKey: "ECGModel.onnx not found in bundle"])
        }

        let sessionOptions = try ORTSessionOptions()
        try sessionOptions.appendCoreMLExecutionProvider(with: ORTCoreMLExecutionProviderOptions())

        ortSession = try ORTSession(env: ortEnv!, modelPath: modelPath,
                                    sessionOptions: sessionOptions)
        isLoaded = true
        print("[ECGClassifier] Model loaded from \(modelPath)")
    }

    /// Classify directly from BLEManager.ecgBuffer — shape [12][up to 1000].
    /// Returns nil if fewer than 1000 samples are available.
    func classify(ecgBuffer: [[Int]], heartRate: Double) -> ECGClassification? {
        guard isLoaded else { return nil }
        guard ecgBuffer.count == Self.numLeads else {
            print("[ECGClassifier] Expected 12 leads, got \(ecgBuffer.count)")
            return nil
        }
        guard let leadLen = ecgBuffer.first?.count, leadLen >= Self.samplesNeeded else {
            let current = ecgBuffer.first?.count ?? 0
            if current % 100 == 0 && current > 0 {
                print("[ECGClassifier] Buffering... \(current)/\(Self.samplesNeeded)")
            }
            return nil
        }

        // Convert ADC → mV and flatten to [12 × 1000] lead-major
        var input = [Float]()
        input.reserveCapacity(Self.numLeads * Self.samplesNeeded)

        for lead in 0..<Self.numLeads {
            let samples = ecgBuffer[lead].suffix(Self.samplesNeeded)
            for s in samples {
                input.append(Float(s) / Self.adcScale)
            }
        }

        // Debug: Lead II peak in mV
        let leadIIPeakMv = ecgBuffer[1].suffix(Self.samplesNeeded).max().map { Float($0) / Self.adcScale } ?? 0

        return runInference(ecgData: input, leadIIPeak: leadIIPeakMv)
    }

    // MARK: - Private inference

    private func runInference(ecgData: [Float], leadIIPeak: Float) -> ECGClassification? {
        guard let ortSession, isLoaded else { return nil }

        do {
            var mutableData = ecgData
            let tensorData = NSMutableData(bytes: &mutableData,
                                           length: mutableData.count * MemoryLayout<Float>.size)
            let shape: [NSNumber] = [1, 12, 1000]
            let inputTensor = try ORTValue(tensorData: tensorData,
                                           elementType: .float,
                                           shape: shape)

            let inputName = try ortSession.inputNames().first ?? "input"
            let outputNames = Set(try ortSession.outputNames())

            let outputs = try ortSession.run(withInputs: [inputName: inputTensor],
                                             outputNames: outputNames,
                                             runOptions: nil)

            let outputName = try ortSession.outputNames().first ?? "output"
            guard let outputValue = outputs[outputName] else {
                throw NSError(domain: "ECG", code: 2,
                              userInfo: [NSLocalizedDescriptionKey: "No output tensor"])
            }

            let rawData = try outputValue.tensorData() as Data
            let logits: [Float] = rawData.withUnsafeBytes { Array($0.bindMemory(to: Float.self)) }

            // Softmax
            let maxLogit = logits.max() ?? 0
            let exps = logits.map { exp($0 - maxLogit) }
            let sumExps = exps.reduce(0, +)
            let probs = exps.map { $0 / sumExps }

            let bestIdx = probs.indices.max(by: { probs[$0] < probs[$1] }) ?? 0
            let topLabel = labels[min(bestIdx, labels.count - 1)]
            let topConfidence = Double(probs[bestIdx])

            var allProbs: [String: Double] = [:]
            for (i, label) in labels.enumerated() where i < probs.count {
                allProbs[label] = Double(probs[i])
            }

            return ECGClassification(
                probabilities: allProbs,
                topLabel: topLabel,
                topConfidence: topConfidence,
                isAbnormal: topLabel != "Normal"
            )
        } catch {
            print("[ECGClassifier] Inference error: \(error.localizedDescription)")
            return nil
        }
    }
}