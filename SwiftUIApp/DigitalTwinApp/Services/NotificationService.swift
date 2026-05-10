import Foundation

/// Service for fetching notifications via the Mobile Engine.
/// Note: Write operations (mark read, delete) are not yet exposed through the engine
/// and would require additional API layer implementation.
actor NotificationService {
    static let shared = NotificationService()

    private struct Key: Hashable {
        let limit: Int
        let unreadOnly: Bool
    }

    private var inflight: [Key: Task<[NotificationInfo], Error>] = [:]

    func fetchNotifications(engine: MobileEngineClient, limit: Int = 50, unreadOnly: Bool = false) async throws -> [NotificationInfo] {
        let key = Key(limit: limit, unreadOnly: unreadOnly)

        if let existing = inflight[key] {
            return try await existing.value
        }

        let task = Task { try await engine.getNotifications(limit: limit, unreadOnly: unreadOnly) }
        inflight[key] = task
        defer { inflight[key] = nil }

        return try await task.value
    }

    // TODO: These operations require additional API layer support
    // func markRead(accessToken: String, id: UUID) async throws { }
    // func markAllRead(accessToken: String) async throws { }
    // func delete(accessToken: String, id: UUID) async throws { }
}

