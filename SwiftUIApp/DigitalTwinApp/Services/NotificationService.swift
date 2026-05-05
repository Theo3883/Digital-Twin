import Foundation

/// Service for fetching notifications via the Mobile Engine.
/// Note: Write operations (mark read, delete) are not yet exposed through the engine
/// and would require additional API layer implementation.
final class NotificationService: Sendable {
    func fetchNotifications(engine: MobileEngineClient, limit: Int = 50, unreadOnly: Bool = false) async throws -> [NotificationInfo] {
        try await engine.getNotifications(limit: limit, unreadOnly: unreadOnly)
    }

    /// Get unread notification count via the engine
    /// Currently returns count of unread items from the fetched list as a workaround
    func fetchUnreadCount(engine: MobileEngineClient) async throws -> Int {
        let notifications = try await engine.getNotifications(limit: 1000, unreadOnly: true)
        return notifications.count
    }

    // TODO: These operations require additional API layer support
    // func markRead(accessToken: String, id: UUID) async throws { }
    // func markAllRead(accessToken: String) async throws { }
    // func delete(accessToken: String, id: UUID) async throws { }
}

