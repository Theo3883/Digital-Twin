import SwiftUI
import GoogleSignIn

@main
struct DigitalTwinAppApp: App {
    @StateObject private var container = AppContainer()
    @StateObject private var ble = BLEManager()

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
                .preferredColorScheme(.dark)
                .task {
                    await container.engine.initialize()
                    BackgroundSyncService.shared.engineWrapperRef = container.engine
                }
                .onOpenURL { url in
                    _ = GoogleSignInService.handleURL(url)
                }
        }
    }
}