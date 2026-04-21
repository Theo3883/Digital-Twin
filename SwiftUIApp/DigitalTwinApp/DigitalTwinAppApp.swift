import SwiftUI
import GoogleSignIn

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
                }
                .onOpenURL { url in
                    _ = GoogleSignInService.handleURL(url)
                }
        }
    }
}