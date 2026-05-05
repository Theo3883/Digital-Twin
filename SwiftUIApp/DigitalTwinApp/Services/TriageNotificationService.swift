import Foundation
import UserNotifications

/// Local notifications when AI + rule triage is warning or critical.
/// Throttled to avoid spam while background triage runs every second.
@MainActor
final class TriageNotificationService {

    static let shared = TriageNotificationService()

    private var lastNotificationDate: Date?
    private let cooldownSeconds: TimeInterval = 30

    private init() {}

    func requestPermission() {
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { granted, error in
            if let error {
                print("[TriageNotification] Permission error: \(error.localizedDescription)")
            } else {
                print("[TriageNotification] Permission \(granted ? "granted" : "denied")")
            }
        }
    }

    /// Call after each triage evaluation. Notifies for warning/critical only.
    func evaluateAndNotify(result: EcgEvaluationResult?) async {
        guard let result else { return }
        guard result.isConnected else { return }

        switch result.triageLevel {
        case .normal:
            return
        case .warning, .critical:
            break
        }

        let settings = await UNUserNotificationCenter.current().notificationSettings()
        guard settings.authorizationStatus == .authorized || settings.authorizationStatus == .provisional else {
            print("[TriageNotification] Skip schedule: notifications not allowed (status=\(settings.authorizationStatus.rawValue))")
            return
        }

        if let last = lastNotificationDate,
           Date().timeIntervalSince(last) < cooldownSeconds {
            return
        }
        lastNotificationDate = Date()

        let content = UNMutableNotificationContent()

        switch result.triageLevel {
        case .critical:
            content.title = "Critical ECG alert"
            content.sound = .defaultCritical
            content.interruptionLevel = .critical
        case .warning:
            content.title = "ECG warning"
            content.sound = .default
            content.interruptionLevel = .timeSensitive
        case .normal:
            return
        }

        var bodyParts: [String] = []

        // If the .NET engine provided an alert message, prefer it verbatim so the in-app
        // Notifications tab (cloud-backed) matches the banner 1:1.
        if let engineAlert = result.alerts.first, !engineAlert.isEmpty {
            // Filter out technical/signal quality alerts - only notify on clinical alerts
            let isTechnicalAlert = engineAlert.lowercased().contains("signal lost") || 
                                 engineAlert.lowercased().contains("flatline") ||
                                 engineAlert.lowercased().contains("electrode")
            
            if !isTechnicalAlert {
                content.body = engineAlert
            } else {
                // Signal quality issue - skip notification but keep in UI log
                return
            }
        } else {
        if let ml = result.mlTopLabel, let conf = result.mlConfidence {
            bodyParts.append("\(ml) (\(Int(conf * 100))%)")
        }

        let hr = result.heartRate
        if hr > 0 {
            if hr > 120 { bodyParts.append("HR \(Int(hr)) bpm (high)") }
            else if hr < 50 { bodyParts.append("HR \(Int(hr)) bpm (low)") }
        }

        if result.spO2 > 0, result.spO2 < 92 {
            bodyParts.append("SpO₂ \(Int(result.spO2))% (low)")
        }

        if !result.alerts.isEmpty {
            // Filter out signal quality alerts
            let clinicalAlerts = result.alerts.filter { alert in
                let lower = alert.lowercased()
                return !lower.contains("signal lost") && !lower.contains("flatline") && !lower.contains("electrode")
            }
            bodyParts.append(contentsOf: clinicalAlerts.prefix(2))
        }

        if bodyParts.isEmpty {
            bodyParts.append("Abnormal ECG pattern detected")
        }

        content.body = bodyParts.joined(separator: " · ")
        }
        content.categoryIdentifier = "ECG_TRIAGE_ALERT"

        let request = UNNotificationRequest(
            identifier: "triage_\(UUID().uuidString)",
            content: content,
            trigger: nil
        )

        do {
            try await UNUserNotificationCenter.current().add(request)
            print("[TriageNotification] Sent: \(content.title) — \(content.body)")
        } catch {
            print("[TriageNotification] Failed: \(error.localizedDescription)")
        }
    }
}
