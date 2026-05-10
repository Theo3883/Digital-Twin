import SwiftUI

struct MainTabView: View {
    @Binding var selectedTab: Int
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    
    // Create repositories once and reuse them — DO NOT recreate on every render
    @State private var chatRepository: EngineChatRepository?
    @State private var environmentRepository: EngineEnvironmentRepository?
    @State private var medicationRepository: EngineMedicationRepository?
    @State private var profileRepository: EngineProfileRepository?
    @State private var ocrRepository: EngineOcrRepository?
    @State private var doctorRepository: EngineDoctorRepository?
    
    // Create ViewModels as @State so they're created once and reused
    @State private var dashboardViewModel: DashboardViewModel?
    @State private var medicalAssistantViewModel: MedicalAssistantViewModel?
    @State private var environmentViewModel: EnvironmentViewModel?
    @State private var profileViewModel: ProfileViewModel?
    @State private var medicationsViewModel: MedicationsViewModel?
    @State private var addMedicationSheetViewModel: AddMedicationSheetViewModel?
    @State private var checkInteractionsSheetViewModel: CheckInteractionsSheetViewModel?
    
    private func initializeViewModelsIfNeeded() {
        guard chatRepository == nil else { return }
        
        // Initialize all repositories
        self.chatRepository = EngineChatRepository(engine: engineWrapper)
        self.environmentRepository = EngineEnvironmentRepository(engine: engineWrapper)
        self.medicationRepository = EngineMedicationRepository(engine: engineWrapper)
        self.profileRepository = EngineProfileRepository(engine: engineWrapper)
        self.ocrRepository = EngineOcrRepository(engine: engineWrapper)
        self.doctorRepository = EngineDoctorRepository(engine: engineWrapper)
        
        // Initialize all ViewModels using the repositories
        let chatRepo = self.chatRepository!
        self.medicalAssistantViewModel = MedicalAssistantViewModel(
            loadHistory: LoadChatHistoryUseCase(repository: chatRepo),
            sendMessage: SendChatMessageUseCase(repository: chatRepo),
            clear: ClearChatHistoryUseCase(repository: chatRepo)
        )
        
        let envRepo = self.environmentRepository!
        self.environmentViewModel = EnvironmentViewModel(
            loadLatest: LoadLatestEnvironmentReadingUseCase(repository: envRepo),
            fetchReading: FetchEnvironmentReadingUseCase(repository: envRepo),
            repository: envRepo
        )
        
        let profRepo = self.profileRepository!
        let docRepo = self.doctorRepository!
        self.profileViewModel = ProfileViewModel(
            repository: profRepo,
            getDoctors: GetAssignedDoctorsUseCase(repository: docRepo)
        )
        
        let medRepo = self.medicationRepository!
        self.medicationsViewModel = MedicationsViewModel(
            loadMedications: LoadMedicationsUseCase(repository: medRepo),
            checkInteractions: CheckMedicationInteractionsUseCase(repository: medRepo),
            discontinue: DiscontinueMedicationUseCase(repository: medRepo),
            preloadedMedications: engineWrapper.medications,
            preloadedInteractions: engineWrapper.medicationInteractions
        )
        
        self.addMedicationSheetViewModel = AddMedicationSheetViewModel(
            searchDrugs: SearchDrugsUseCase(repository: medRepo),
            addMedication: AddMedicationUseCase(repository: medRepo)
        )
        
        self.checkInteractionsSheetViewModel = CheckInteractionsSheetViewModel(
            searchDrugs: SearchDrugsUseCase(repository: medRepo),
            checkInteractions: CheckMedicationInteractionsUseCase(repository: medRepo)
        )
        
        self.dashboardViewModel = DashboardViewModel(
            getSnapshot: GetDashboardSnapshotUseCase(
                repository: EngineDashboardRepository(engine: engineWrapper)
            )
        )
    }

    var body: some View {
        TabView(selection: $selectedTab) {
            Tab("Home", systemImage: "house.fill", value: 0) {
                ZStack {
                    MeshGradientBackground()
                    if let vm = dashboardViewModel {
                        DashboardView(
                            selectedTab: $selectedTab,
                            viewModel: vm
                        )
                    }
                }
            }
            Tab("ECG", systemImage: "waveform.path.ecg", value: 1) {
                ZStack {
                    MeshGradientBackground()
                    NavigationStack {
                        if let vm = EcgMonitorViewModelFactory.shared {
                            EcgMonitorView(viewModel: vm)
                                .background(Color.clear)
                                .liquidGlassNavigationStyle()
                        } else {
                            VStack(spacing: 12) {
                                ProgressView()
                                Text("Preparing ECG…")
                                    .foregroundColor(.white.opacity(0.7))
                            }
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                        }
                    }
                    .background(Color.clear)
                    .toolbarBackground(.hidden, for: .navigationBar)
                }
            }
            Tab("Assistant", systemImage: "brain.head.profile", value: 2) {
                ZStack {
                    MeshGradientBackground()
                    if let vm = medicalAssistantViewModel {
                        MedicalAssistantView(viewModel: vm)
                    }
                }
            }
            Tab("Air", systemImage: "aqi.medium", value: 3) {
                ZStack {
                    MeshGradientBackground()
                    if let vm = environmentViewModel {
                        EnvironmentView(viewModel: vm)
                    }
                }
            }
            Tab("Patient", systemImage: "person.text.rectangle", value: 4) {
                ZStack {
                    if let profileVM = profileViewModel,
                       let medsVM = medicationsViewModel,
                       let addVM = addMedicationSheetViewModel,
                       let checkVM = checkInteractionsSheetViewModel,
                       let profRepo = profileRepository,
                       let ocrRepo = ocrRepository {
                        PatientView(
                            profileVM: profileVM,
                            medsVM: medsVM,
                            addSheetViewModel: addVM,
                            checkSheetViewModel: checkVM,
                            profileRepository: profRepo,
                            ocrRepository: ocrRepo
                        )
                    }
                }
            }
        }
        .tint(LiquidGlass.tealPrimary)
        .onAppear {
            initializeViewModelsIfNeeded()
        }
    }
}

