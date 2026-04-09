import SwiftUI

struct ContentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var selectedTab = 0
    @State private var showSyncGate = true
    
    var body: some View {
        ZStack {
            Group {
                if !engineWrapper.isInitialized {
                    LoadingView(message: "Initializing DigitalTwin...")
                } else if !engineWrapper.isAuthenticated {
                    AuthenticationView()
                } else {
                    MainTabView(selectedTab: $selectedTab)
                }
            }
            
            // Sync Gate overlay
            if showSyncGate && engineWrapper.isAuthenticated {
                SyncGateView(isVisible: $showSyncGate)
            }
        }
        .alert("Error", isPresented: .constant(engineWrapper.errorMessage != nil)) {
            Button("OK") {
                engineWrapper.errorMessage = nil
            }
        } message: {
            Text(engineWrapper.errorMessage ?? "")
        }
    }
}