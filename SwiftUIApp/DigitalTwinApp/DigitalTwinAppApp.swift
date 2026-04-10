import SwiftUI
import GoogleSignIn

@main
struct DigitalTwinAppApp: App {
    @StateObject private var container = AppContainer()
    
    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(container)
                .environmentObject(container.engine)
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