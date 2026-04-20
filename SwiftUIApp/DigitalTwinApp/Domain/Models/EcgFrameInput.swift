import Foundation

struct EcgFrameInput: Codable {
    let samples: [Double]           // Lead II samples for domain rules (1-lead)
    let spO2: Double
    let heartRate: Double
    let timestamp: Date?
    /// CoreML ECGClassifier probabilities ("1dAVb","RBBB","LBBB","SB","AF","ST"). Nil when buffer not ready.
    let mlScores: [String: Double]?
    /// Number of leads in samples: 1 (domain only) or 12 (full 12-lead).
    let numLeads: Int?
}


