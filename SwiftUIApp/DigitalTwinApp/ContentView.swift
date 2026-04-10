import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var container: AppContainer
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
                } else if engineWrapper.isHydratingAfterAuth {
                    LoadingView(message: "Loading your profile...")
                } else if engineWrapper.patientProfile == nil {
                    // Only force profile setup when we *know* the cloud doesn't have one.
                    // If cloud has a profile, this should have been seeded locally during auth.
                    if engineWrapper.hasCloudProfile {
                        LoadingView(message: "Loading your data...")
                    } else {
                        ProfileSetupGateView()
                        .sheet(isPresented: $container.shouldPresentProfileEdit) {
                            ProfileEditSheet(
                                viewModel: ProfileEditSheetViewModel(
                                    repository: EngineProfileRepository(engine: engineWrapper),
                                    patient: nil
                                )
                            )
                        }
                    }
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