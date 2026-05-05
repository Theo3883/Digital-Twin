import Foundation

struct SleepSessionInput: Codable {
    let startTime: Date
    let endTime: Date
    let durationMinutes: Int
    let qualityScore: Double?
}

