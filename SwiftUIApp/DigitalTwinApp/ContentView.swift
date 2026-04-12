import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var container: AppContainer
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @State private var selectedTab = 0
    @State private var showSyncGate = true
    @State private var didAutoPresentProfileSetup = false
    @State private var shouldOpenPatientSetupAfterUserSave = false
    
    var body: some View {
        ZStack {
            MeshGradientBackground()
            
            Group {
                if !engineWrapper.isInitialized {
                    LoadingView(message: "Initializing DigitalTwin...")
                } else if !engineWrapper.isAuthenticated {
                    AuthenticationView()
                } else if engineWrapper.isHydratingAfterAuth {
                    LoadingView(message: "Loading your profile...")
                } else if engineWrapper.patientProfile == nil {
                    ProfileSetupGateView()
                        .onAppear {
                            guard !didAutoPresentProfileSetup else { return }
                            didAutoPresentProfileSetup = true

                            if engineWrapper.hasCloudProfile {
                                container.shouldPresentPatientProfileEdit = true
                            } else {
                                container.shouldPresentUserProfileEdit = true
                            }
                        }
                        .sheet(isPresented: $container.shouldPresentUserProfileEdit) {
                            ProfileEditSheet(
                                viewModel: ProfileEditSheetViewModel(
                                    repository: EngineProfileRepository(engine: engineWrapper),
                                    user: engineWrapper.currentUser
                                ),
                                isMandatorySetup: true,
                                onCancel: {
                                    let _ = await engineWrapper.resetLocalData()
                                    await engineWrapper.signOut()
                                    container.shouldPresentUserProfileEdit = false
                                    container.shouldPresentPatientProfileEdit = false
                                    shouldOpenPatientSetupAfterUserSave = false
                                },
                                onSave: {
                                    shouldOpenPatientSetupAfterUserSave = true
                                }
                            )
                        }
                        .sheet(isPresented: $container.shouldPresentPatientProfileEdit) {
                            PatientProfileEditSheet(
                                viewModel: PatientProfileEditSheetViewModel(
                                    repository: EngineProfileRepository(engine: engineWrapper),
                                    patient: engineWrapper.patientProfile
                                ),
                                isMandatorySetup: true,
                                onCancel: {
                                    let _ = await engineWrapper.resetLocalData()
                                    await engineWrapper.signOut()
                                    container.shouldPresentUserProfileEdit = false
                                    container.shouldPresentPatientProfileEdit = false
                                    shouldOpenPatientSetupAfterUserSave = false
                                }
                            )
                        }
                        .onChange(of: container.shouldPresentUserProfileEdit) { _, isPresented in
                            guard !isPresented else { return }
                            guard shouldOpenPatientSetupAfterUserSave else { return }
                            guard engineWrapper.patientProfile == nil else {
                                shouldOpenPatientSetupAfterUserSave = false
                                return
                            }

                            shouldOpenPatientSetupAfterUserSave = false
                            container.shouldPresentPatientProfileEdit = true
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
        .onChange(of: engineWrapper.isAuthenticated) { _, isAuthenticated in
            if !isAuthenticated {
                didAutoPresentProfileSetup = false
                shouldOpenPatientSetupAfterUserSave = false
                container.shouldPresentUserProfileEdit = false
                container.shouldPresentPatientProfileEdit = false
            }
        }
        .onChange(of: engineWrapper.patientProfile != nil) { _, hasPatientProfile in
            if hasPatientProfile {
                didAutoPresentProfileSetup = false
                shouldOpenPatientSetupAfterUserSave = false
                container.shouldPresentUserProfileEdit = false
                container.shouldPresentPatientProfileEdit = false
            }
        }
    }
}