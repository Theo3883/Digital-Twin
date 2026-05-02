import Foundation

struct EcgEvaluationResult: Codable {
    let triageResult: Int   // 0=Normal, 1=Warning, 2=Critical
    let alerts: [String]
    let heartRate: Double
    let spO2: Double

    // ── ML fields — populated by EcgMonitorViewModel after ONNX inference ──
    /// Top XceptionTime label, e.g. "AFib" or "Normal".
    var mlTopLabel: String?
    /// Confidence of the top prediction (0.0–1.0)
    var mlConfidence: Double?
    /// XceptionTime (PTB-XL) probabilities keyed by label ("Normal", "AFib", "PVC", etc.).
    var mlProbabilities: [String: Double]?

    // ── Connection & signal quality ──────────────────────────────────────────
    var signalQualityPassed: Bool
    var isConnected: Bool

    // Memberwise init with defaults for optional ML fields
    init(triageResult: Int, alerts: [String], heartRate: Double, spO2: Double,
         mlTopLabel: String? = nil, mlConfidence: Double? = nil,
         mlProbabilities: [String: Double]? = nil,
         signalQualityPassed: Bool = true, isConnected: Bool = true) {
        self.triageResult = triageResult
        self.alerts = alerts
        self.heartRate = heartRate
        self.spO2 = spO2
        self.mlTopLabel = mlTopLabel
        self.mlConfidence = mlConfidence
        self.mlProbabilities = mlProbabilities
        self.signalQualityPassed = signalQualityPassed
        self.isConnected = isConnected
    }

    // ── Convenience ──────────────────────────────────────────────────────────
    var triageDisplay: String {
        switch triageResult {
        case 0: return "Normal"
        case 1: return "Warning"
        case 2: return "Critical"
        default: return "Unknown"
        }
    }

    var isCritical: Bool { triageResult == 2 }
    var isWarning:  Bool { triageResult == 1 }

    /// For UI / notifications: map integer triage to levels.
    enum TriageLevel: Int, Sendable {
        case normal = 0
        case warning = 1
        case critical = 2
    }

    var triageLevel: TriageLevel {
        TriageLevel(rawValue: triageResult) ?? .normal
    }

    /// Human-readable ML label with confidence, e.g. "AF (87%)"
    var mlSummary: String {
        guard let label = mlTopLabel, let conf = mlConfidence else { return "Analyzing..." }
        let pct = Int(conf * 100)
        return "\(label) (\(pct)%)"
    }

    /// Disconnected sentinel — used when ESP32 is not connected
    static let disconnected = EcgEvaluationResult(
        triageResult: 0, alerts: [], heartRate: 0, spO2: 0,
        signalQualityPassed: false, isConnected: false
    )
}
