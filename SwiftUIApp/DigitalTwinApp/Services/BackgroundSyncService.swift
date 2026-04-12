import BackgroundTasks
import Foundation
import UIKit
import UserNotifications

/// Service for managing background sync operations
@MainActor
final class BackgroundSyncService: ObservableObject {
    
    // MARK: - Properties
    
    static let shared = BackgroundSyncService()
    
    private let backgroundTaskIdentifier = "com.digitaltwin.mobile.backgroundsync"
    private let healthKitService = HealthKitService()
    
    @Published var isBackgroundSyncEnabled = true
    @Published var lastBackgroundSync: Date?
    @Published var backgroundSyncStatus: BackgroundSyncStatus = .idle
    
    // MARK: - Initialization
    
    private init() {
        registerBackgroundTasks()
        setupNotificationObservers()
    }
    
    // MARK: - Background Task Registration
    
    /// Register background task handlers
    private func registerBackgroundTasks() {
        BGTaskScheduler.shared.register(
            forTaskWithIdentifier: backgroundTaskIdentifier,
            using: nil
        ) { [weak self] task in
            // BGTaskScheduler callback is not guaranteed to be on the main actor.
            Task { @MainActor in
                self?.handleBackgroundSync(task: task as! BGAppRefreshTask)
            }
        }
    }
    
    /// Schedule background sync task
    func scheduleBackgroundSync() {
        guard isBackgroundSyncEnabled else { return }
        
        let request = BGAppRefreshTaskRequest(identifier: backgroundTaskIdentifier)
        request.earliestBeginDate = Date(timeIntervalSinceNow: 60 * 60) // 1 hour from now
        
        do {
            try BGTaskScheduler.shared.submit(request)
            print("[BackgroundSyncService] Background sync scheduled")
        } catch {
            print("[BackgroundSyncService] Failed to schedule background sync: \(error)")
        }
    }
    
    /// Cancel scheduled background sync
    func cancelBackgroundSync() {
        BGTaskScheduler.shared.cancel(taskRequestWithIdentifier: backgroundTaskIdentifier)
        print("[BackgroundSyncService] Background sync cancelled")
    }
    
    // MARK: - Background Sync Handler
    
    private func handleBackgroundSync(task: BGAppRefreshTask) {
        print("[BackgroundSyncService] Background sync started")
        
        backgroundSyncStatus = .syncing
        
        // Schedule the next background sync
        scheduleBackgroundSync()
        
        // Create expiration handler
        task.expirationHandler = { [weak self] in
            print("[BackgroundSyncService] Background sync expired")
            Task { @MainActor in
                self?.backgroundSyncStatus = .failed
            }
            task.setTaskCompleted(success: false)
        }
        
        // Perform the sync operation
        Task {
            let success = await self.performBackgroundSync()

            await MainActor.run {
                self.backgroundSyncStatus = success ? .completed : .failed
                self.lastBackgroundSync = Date()
                task.setTaskCompleted(success: success)
            }

            print("[BackgroundSyncService] Background sync completed: \(success)")
        }
    }
    
    // MARK: - Sync Operations
    
    /// Perform background sync operation
    private func performBackgroundSync() async -> Bool {
        guard let engineWrapper = await getEngineWrapper() else {
            print("[BackgroundSyncService] Engine wrapper not available")
            return false
        }

        guard engineWrapper.isCloudAuthenticated else {
            print("[BackgroundSyncService] Skipping cloud sync: cloud authentication is not ready")
            return true
        }
        
        // 1. Sync HealthKit data if authorized
        if healthKitService.isAuthorized {
            await syncHealthKitData(engineWrapper: engineWrapper)
        }

        // 2. Push local changes to cloud
        let pushSuccess = await engineWrapper.pushLocalChanges()
        if !pushSuccess {
            print("[BackgroundSyncService] Failed to push local changes")
            return false
        }

        // 3. Perform full sync (bidirectional)
        let syncSuccess = await engineWrapper.performSync()
        if !syncSuccess {
            print("[BackgroundSyncService] Failed to perform full sync")
            return false
        }

        // 4. Rebuild medications + environment caches so they are hot on next foreground
        await engineWrapper.loadMedications()
        await engineWrapper.loadLatestEnvironmentReading()

        // 5. Send local notification if significant changes
        await sendSyncNotificationIfNeeded()

        return true
    }
    
    /// Sync HealthKit data to the engine
    private func syncHealthKitData(engineWrapper: MobileEngineWrapper) async {
        do {
            // Get HealthKit data from the last 24 hours
            let endDate = Date()
            let startDate = Calendar.current.date(byAdding: .day, value: -1, to: endDate) ?? endDate
            
            let healthKitVitals = try await healthKitService.readVitalSigns(from: startDate, to: endDate)
            
            // Convert to VitalSignInput and record in engine
            for vital in healthKitVitals {
                let vitalInput = VitalSignInput(
                    type: vital.type,
                    value: vital.value,
                    unit: vital.unit,
                    source: vital.source,
                    timestamp: vital.timestamp
                )
                
                let _ = await engineWrapper.recordVitalSign(vitalInput)
            }
            
            print("[BackgroundSyncService] Synced \(healthKitVitals.count) HealthKit vitals")
            
        } catch {
            print("[BackgroundSyncService] HealthKit sync error: \(error)")
        }
    }
    
    /// Weak reference to the shared engine wrapper, set from the app on launch
    weak var engineWrapperRef: MobileEngineWrapper?

    /// Get the engine wrapper from the stored reference
    private func getEngineWrapper() async -> MobileEngineWrapper? {
        return engineWrapperRef
    }
    
    /// Send notification if sync resulted in significant changes
    private func sendSyncNotificationIfNeeded() async {
        // Check if we should notify the user about sync results
        // This could be based on new vital signs, sync errors, etc.
        
        let notificationCenter = UNUserNotificationCenter.current()
        
        // Check notification permissions
        let settings = await notificationCenter.notificationSettings()
        guard settings.authorizationStatus == .authorized else { return }
        
        // Create notification content
        let content = UNMutableNotificationContent()
        content.title = "Health Data Synced"
        content.body = "Your health data has been synchronized with the cloud."
        content.sound = .default
        content.badge = 1
        
        // Create notification request
        let request = UNNotificationRequest(
            identifier: "background-sync-\(UUID().uuidString)",
            content: content,
            trigger: nil // Deliver immediately
        )
        
        do {
            try await notificationCenter.add(request)
            print("[BackgroundSyncService] Sync notification sent")
        } catch {
            print("[BackgroundSyncService] Failed to send notification: \(error)")
        }
    }
    
    // MARK: - App Lifecycle Observers
    
    private func setupNotificationObservers() {
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(appDidEnterBackground),
            name: UIApplication.didEnterBackgroundNotification,
            object: nil
        )
        
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(appWillEnterForeground),
            name: UIApplication.willEnterForegroundNotification,
            object: nil
        )
    }
    
    @objc private func appDidEnterBackground() {
        if isBackgroundSyncEnabled {
            scheduleBackgroundSync()
        }
    }
    
    @objc private func appWillEnterForeground() {
        // Update sync status when app comes to foreground
        backgroundSyncStatus = .idle
    }
    
    // MARK: - Manual Sync
    
    /// Perform manual sync (called from UI)
    func performManualSync() async -> Bool {
        backgroundSyncStatus = .syncing
        
        let success = await performBackgroundSync()
        
        await MainActor.run {
            self.backgroundSyncStatus = success ? .completed : .failed
            if success {
                self.lastBackgroundSync = Date()
            }
        }
        
        return success
    }
    
    // MARK: - Settings
    
    /// Enable or disable background sync
    func setBackgroundSyncEnabled(_ enabled: Bool) {
        isBackgroundSyncEnabled = enabled
        
        if enabled {
            scheduleBackgroundSync()
        } else {
            cancelBackgroundSync()
        }
        
        // Save preference
        UserDefaults.standard.set(enabled, forKey: "backgroundSyncEnabled")
    }
    
    /// Load background sync preference
    func loadBackgroundSyncPreference() {
        isBackgroundSyncEnabled = UserDefaults.standard.bool(forKey: "backgroundSyncEnabled")
    }
}

// MARK: - Background Sync Status

enum BackgroundSyncStatus {
    case idle
    case syncing
    case completed
    case failed
    
    var displayName: String {
        switch self {
        case .idle:
            return "Ready"
        case .syncing:
            return "Syncing..."
        case .completed:
            return "Completed"
        case .failed:
            return "Failed"
        }
    }
    
    var color: UIColor {
        switch self {
        case .idle:
            return .systemGray
        case .syncing:
            return .systemBlue
        case .completed:
            return .systemGreen
        case .failed:
            return .systemRed
        }
    }
}

// MARK: - Notification Permissions Helper

extension BackgroundSyncService {
    
    /// Request notification permissions for background sync updates
    func requestNotificationPermissions() async -> Bool {
        let notificationCenter = UNUserNotificationCenter.current()
        
        do {
            let granted = try await notificationCenter.requestAuthorization(
                options: [.alert, .sound, .badge]
            )
            
            print("[BackgroundSyncService] Notification permissions granted: \(granted)")
            return granted
            
        } catch {
            print("[BackgroundSyncService] Failed to request notification permissions: \(error)")
            return false
        }
    }
    
    /// Check current notification authorization status
    func checkNotificationPermissions() async -> UNAuthorizationStatus {
        let notificationCenter = UNUserNotificationCenter.current()
        let settings = await notificationCenter.notificationSettings()
        return settings.authorizationStatus
    }
}