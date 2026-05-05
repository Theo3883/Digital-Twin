import Foundation

struct SleepSessionInfo: Codable, Identifiable {
    let id: UUID
    let patientId: UUID?
    let startTime: Date
    let endTime: Date
    let durationMinutes: Int
    let qualityScore: Double
    let createdAt: Date?
    let isSynced: Bool

    enum CodingKeys: String, CodingKey {
        case id
        case patientId
        case startTime
        case endTime
        case durationMinutes
        case qualityScore
        case createdAt
        case isSynced
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        patientId = try container.decodeIfPresent(UUID.self, forKey: .patientId)
        startTime = try container.decode(Date.self, forKey: .startTime)
        endTime = try container.decode(Date.self, forKey: .endTime)
        durationMinutes = try container.decodeIfPresent(Int.self, forKey: .durationMinutes) ?? 0
        qualityScore = try container.decodeIfPresent(Double.self, forKey: .qualityScore) ?? 0
        createdAt = try container.decodeIfPresent(Date.self, forKey: .createdAt)
        isSynced = try container.decodeIfPresent(Bool.self, forKey: .isSynced) ?? false
    }

    var durationFormatted: String {
        let hours = durationMinutes / 60
        let mins = durationMinutes % 60
        if hours > 0 {
            return "\(hours)h \(mins)m"
        }
        return "\(mins)m"
    }

    var qualityDisplay: String {
        if durationMinutes >= 420 { return "Optimal" }
        if durationMinutes >= 360 { return "Good" }
        if durationMinutes >= 300 { return "Fair" }
        return "Poor"
    }
}

