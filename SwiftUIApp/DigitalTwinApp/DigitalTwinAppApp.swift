import SwiftUI
import GoogleSignIn

@main
struct DigitalTwinAppApp: App {
    @StateObject private var engineWrapper = MobileEngineWrapper()
    
    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(engineWrapper)
                .preferredColorScheme(.dark)
                .task {
                    await engineWrapper.initialize()
                    BackgroundSyncService.shared.engineWrapperRef = engineWrapper
                }
                .onOpenURL { url in
                    GoogleSignInService.handleURL(url)
                }
        }
    }
}