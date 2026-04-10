import Foundation

struct SleepSessionInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID
    let startTime: Date
    let endTime: Date
    let durationMinutes: Int
    let qualityScore: Double
    let createdAt: Date?
    let isSynced: Bool

    var durationFormatted: String {
        let hours = durationMinutes / 60
        let mins = durationMinutes % 60
        if hours > 0 {
            return "\(hours)h \(mins)m"
        }
        return "\(mins)m"
    }

    var qualityDisplay: String {
        if qualityScore >= 80 { return "Excellent" }
        if qualityScore >= 60 { return "Good" }
        if qualityScore >= 40 { return "Fair" }
        return "Poor"
    }
}

