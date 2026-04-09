import Foundation

struct EcgFrameInput: Codable {
    let samples: [Double]
    let spO2: Double
    let heartRate: Double
    let timestamp: Date?
}

