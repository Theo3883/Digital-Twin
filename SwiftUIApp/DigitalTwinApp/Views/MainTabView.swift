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

    private var doctorRepository: EngineDoctorRepository {
        EngineDoctorRepository(engine: engineWrapper)
    }

    var body: some View {
        TabView(selection: $selectedTab) {
            Tab("Home", systemImage: "house.fill", value: 0) {
                ZStack {
                    MeshGradientBackground()
                    DashboardView(
                        selectedTab: $selectedTab,
                        viewModel: DashboardViewModel(
                            getSnapshot: GetDashboardSnapshotUseCase(
                                repository: EngineDashboardRepository(engine: engineWrapper)
                            )
                        )
                    )
                }
            }
            Tab("ECG", systemImage: "waveform.path.ecg", value: 1) {
                ZStack {
                    MeshGradientBackground()
                    EcgMonitorView(
                        viewModel: EcgMonitorViewModel(
                            repository: ecgRepository,
                            evaluate: EvaluateEcgFrameUseCase(repository: ecgRepository)
                        )
                    )
                }
            }
            Tab("Assistant", systemImage: "brain.head.profile", value: 2) {
                ZStack {
                    MeshGradientBackground()
                    MedicalAssistantView(
                        viewModel: MedicalAssistantViewModel(
                            loadHistory: LoadChatHistoryUseCase(repository: chatRepository),
                            sendMessage: SendChatMessageUseCase(repository: chatRepository),
                            clear: ClearChatHistoryUseCase(repository: chatRepository)
                        )
                    )
                }
            }
            Tab("Air", systemImage: "aqi.medium", value: 3) {
                ZStack {
                    MeshGradientBackground()
                    EnvironmentView(
                        viewModel: EnvironmentViewModel(
                            loadLatest: LoadLatestEnvironmentReadingUseCase(repository: environmentRepository),
                            fetchReading: FetchEnvironmentReadingUseCase(repository: environmentRepository),
                            repository: environmentRepository
                        )
                    )
                }
            }
            Tab("Patient", systemImage: "person.text.rectangle", value: 4) {
                ZStack {
                    PatientView(
                        profileVM: ProfileViewModel(
                            repository: profileRepository,
                            getDoctors: GetAssignedDoctorsUseCase(repository: doctorRepository)
                        ),
                        medsVM: MedicationsViewModel(
                            loadMedications: LoadMedicationsUseCase(repository: medicationRepository),
                            checkInteractions: CheckMedicationInteractionsUseCase(repository: medicationRepository),
                            discontinue: DiscontinueMedicationUseCase(repository: medicationRepository),
                            preloadedMedications: engineWrapper.medications,
                            preloadedInteractions: engineWrapper.medicationInteractions
                        ),
                        addSheetViewModel: AddMedicationSheetViewModel(
                            searchDrugs: SearchDrugsUseCase(repository: medicationRepository),
                            addMedication: AddMedicationUseCase(repository: medicationRepository)
                        ),
                        profileRepository: profileRepository,
                        ocrRepository: ocrRepository
                    )
                }
            }
        }
        .tint(LiquidGlass.tealPrimary)
    }
}

