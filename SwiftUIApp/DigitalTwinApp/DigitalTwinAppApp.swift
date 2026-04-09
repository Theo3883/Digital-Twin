import SwiftUI
import GoogleSignIn

@main
struct DigitalTwinAppApp: App {
    @StateObject private var engineWrapper = MobileEngineWrapper()
    
    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(engineWrapper)
                .task {
                    await engineWrapper.initialize()
                }
                .onOpenURL { url in
                    GoogleSignInService.handleURL(url)
                }
        }
    }
}