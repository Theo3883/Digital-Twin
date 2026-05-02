import Foundation
import UserNotifications

/// Without this delegate, iOS delivers local notifications while the app is **foregrounded**
/// but does **not** show banners or play sounds — `add(_:)` still succeeds, which matches
/// “[TriageNotification] Sent” in the console with nothing visible on screen.
final class AppNotificationCenterDelegate: NSObject, UNUserNotificationCenterDelegate {

    /// `UNUserNotificationCenter` retains its `delegate` weakly; keep one live instance for the process.
    nonisolated(unsafe) static let shared = AppNotificationCenterDelegate()

    private override init() {
        super.init()
    }

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .sound, .badge, .list])
    }
}
