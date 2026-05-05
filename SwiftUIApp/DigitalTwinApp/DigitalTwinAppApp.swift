import SwiftUI
import GoogleSignIn
import UserNotifications

@main
struct DigitalTwinAppApp: App {
    @StateObject private var container = AppContainer()
    @StateObject private var ble = BLEManager()
    @StateObject private var esp32MinuteVitals = Esp32MinuteVitalsPersistenceService.shared

    init() {
        UIScrollView.appearance().showsVerticalScrollIndicator = false
        UIScrollView.appearance().showsHorizontalScrollIndicator = false
        UITableView.appearance().showsVerticalScrollIndicator = false
        UITableView.appearance().showsHorizontalScrollIndicator = false
        UICollectionView.appearance().showsVerticalScrollIndicator = false
        UICollectionView.appearance().showsHorizontalScrollIndicator = false

        let viewEcg = UNNotificationAction(
            identifier: "VIEW_ECG",
            title: "View ECG",
            options: [.foreground]
        )
        let category = UNNotificationCategory(
            identifier: "ECG_TRIAGE_ALERT",
            actions: [viewEcg],
            intentIdentifiers: [],
            options: []
        )
        UNUserNotificationCenter.current().setNotificationCategories([category])
        UNUserNotificationCenter.current().delegate = AppNotificationCenterDelegate.shared
        TriageNotificationService.shared.requestPermission()
    }
    
    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(container)
                .environmentObject(container.engine)
                .environmentObject(ble)
                .environmentObject(esp32MinuteVitals)
                .preferredColorScheme(.dark)
                .task {
                    await container.engine.initialize()
                    esp32MinuteVitals.attach(bleManager: ble, engine: container.engine)
                    BackgroundSyncService.shared.engineWrapperRef = container.engine

                    // Build the shared EcgMonitorViewModel and attach the background triage service.
                    // Triage will auto-start/stop based on BLE connection state,
                    // with no requirement for the user to be on the ECG page.
                    let ecgViewModel = EcgMonitorViewModelFactory.makeShared(engine: container.engine)
                    BackgroundECGTriageService.shared.attach(ble: ble, viewModel: ecgViewModel)
                }
                .onOpenURL { url in
                    _ = GoogleSignInService.handleURL(url)
                }
        }
    }
}