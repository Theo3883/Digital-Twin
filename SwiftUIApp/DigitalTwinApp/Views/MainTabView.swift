import SwiftUI

struct MainTabView: View {
    @Binding var selectedTab: Int
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    
    private var chatRepository: EngineChatRepository {
        EngineChatRepository(engine: engineWrapper)
    }
    
    private var environmentRepository: EngineEnvironmentRepository {
        EngineEnvironmentRepository(engine: engineWrapper)
    }
    
    private var medicationRepository: EngineMedicationRepository {
        EngineMedicationRepository(engine: engineWrapper)
    }
    
    private var profileRepository: EngineProfileRepository {
        EngineProfileRepository(engine: engineWrapper)
    }
    
    private var ocrRepository: EngineOcrRepository {
        EngineOcrRepository(engine: engineWrapper)
    }
    
    private var ecgRepository: EngineEcgRepository {
        EngineEcgRepository(engine: engineWrapper)
    }

    var body: some View {
        TabView(selection: $selectedTab) {
            Tab("Home", systemImage: "house.fill", value: 0) {
                DashboardView(
                    selectedTab: $selectedTab,
                    viewModel: DashboardViewModel(
                        getSnapshot: GetDashboardSnapshotUseCase(
                            repository: EngineDashboardRepository(engine: engineWrapper)
                        )
                    )
                )
            }
            Tab("ECG", systemImage: "waveform.path.ecg", value: 1) {
                EcgMonitorView(
                    viewModel: EcgMonitorViewModel(
                        repository: ecgRepository,
                        evaluate: EvaluateEcgFrameUseCase(repository: ecgRepository)
                    )
                )
            }
            Tab("Assistant", systemImage: "bubble.left.fill", value: 2) {
                MedicalAssistantView(
                    viewModel: MedicalAssistantViewModel(
                        loadHistory: LoadChatHistoryUseCase(repository: chatRepository),
                        sendMessage: SendChatMessageUseCase(repository: chatRepository),
                        clear: ClearChatHistoryUseCase(repository: chatRepository)
                    )
                )
            }
            Tab("Air", systemImage: "leaf.fill", value: 3) {
                EnvironmentView(
                    viewModel: EnvironmentViewModel(
                        loadLatest: LoadLatestEnvironmentReadingUseCase(repository: environmentRepository),
                        fetchReading: FetchEnvironmentReadingUseCase(repository: environmentRepository),
                        repository: environmentRepository
                    )
                )
            }
            Tab("Meds", systemImage: "pills.fill", value: 4) {
                MedicationsView(
                    viewModel: MedicationsViewModel(
                        loadMedications: LoadMedicationsUseCase(repository: medicationRepository),
                        checkInteractions: CheckMedicationInteractionsUseCase(repository: medicationRepository),
                        discontinue: DiscontinueMedicationUseCase(repository: medicationRepository)
                    ),
                    addSheetViewModel: AddMedicationSheetViewModel(
                        searchDrugs: SearchDrugsUseCase(repository: medicationRepository),
                        addMedication: AddMedicationUseCase(repository: medicationRepository)
                    )
                )
            }
            Tab("Profile", systemImage: "person.fill", value: 5) {
                ProfileView(
                    viewModel: ProfileViewModel(repository: profileRepository),
                    repository: profileRepository,
                    ocrRepository: ocrRepository
                )
            }
            .defaultVisibility(.hidden, for: .tabBar)
        }
        .tint(LiquidGlass.tealPrimary)
    }
}

