import Foundation

struct NotificationInfo: Identifiable, Codable {
    let id: UUID
    let title: String
    let body: String
    let type: Int
    let severity: Int
    let patientId: UUID?
    let actorUserId: UUID?
    let actorName: String?
    let createdAt: Date
    let readAt: Date?

    var isUnread: Bool { readAt == nil }
}
