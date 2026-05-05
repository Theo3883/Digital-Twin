import Foundation

@MainActor
final class MedicationsViewModel: ObservableObject {
    @Published private(set) var medications: [MedicationInfo] = []
    @Published private(set) var interactions: [MedicationInteractionInfo] = []
    @Published var drugSearchResults: [DrugSearchResult] = []

    @Published var isAddSheetPresented: Bool = false
    @Published var isCheckSheetPresented: Bool = false
    @Published var isInteractionsSheetPresented: Bool = false
    @Published var endReason: String = ""
    @Published var isEndReasonDialogPresented: Bool = false
    @Published var selectedMedication: MedicationInfo?

    /// True once data has been loaded at least once — prevents redundant fetches on re-navigation.
    @Published private(set) var hasLoaded: Bool = false

    private let loadMedications: LoadMedicationsUseCase
    private let checkInteractions: CheckMedicationInteractionsUseCase
    private let discontinue: DiscontinueMedicationUseCase

    init(
        loadMedications: LoadMedicationsUseCase,
        checkInteractions: CheckMedicationInteractionsUseCase,
        discontinue: DiscontinueMedicationUseCase,
        preloadedMedications: [MedicationInfo] = [],
        preloadedInteractions: [MedicationInteractionInfo] = []
    ) {
        self.loadMedications = loadMedications
        self.checkInteractions = checkInteractions
        self.discontinue = discontinue
        if !preloadedMedications.isEmpty {
            self.medications = preloadedMedications
            self.interactions = preloadedInteractions
            self.hasLoaded = true
        }
    }

    var activeMedications: [MedicationInfo] {
        medications.filter { $0.isActive }
    }

    var inactiveMedications: [MedicationInfo] {
        medications.filter { !$0.isActive }
    }

    /// Load only if not already loaded — call this from view `.task` to avoid re-fetching on re-navigation.
    func loadIfNeeded() async {
        guard !hasLoaded else { return }
        await refresh()
    }

    func refresh() async {
        medications = await loadMedications()
        await refreshInteractions()
        hasLoaded = true
    }

    func refreshInteractions() async {
        let rxCuis = medications
            .filter { $0.isActive && $0.rxCui != nil }
            .compactMap { $0.rxCui }

        guard rxCuis.count >= 2 else {
            interactions = []
            return
        }

        interactions = await checkInteractions(rxCuis: rxCuis)
    }

    func promptEndMedication(_ med: MedicationInfo) {
        selectedMedication = med
        isEndReasonDialogPresented = true
    }

    func confirmEndMedication() {
        guard let med = selectedMedication else { return }
        let reason = endReason.trimmingCharacters(in: .whitespacesAndNewlines)

        Task {
            let _ = await discontinue(id: med.id, reason: reason.isEmpty ? nil : reason)
            endReason = ""
            isEndReasonDialogPresented = false
            selectedMedication = nil
            await refresh()
        }
    }

    func removeMedication(_ med: MedicationInfo) {
        Task {
            let _ = await discontinue(id: med.id, reason: "Removed by user")
            await refresh()
        }
    }

    func applyManualCheckResults(_ results: [MedicationInteractionInfo]) {
        let activeRxCuis = Set(
            activeMedications
                .compactMap { $0.rxCui?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
                .filter { !$0.isEmpty }
        )

        guard activeRxCuis.count >= 2 else {
            interactions = []
            return
        }

        interactions = results.filter {
            activeRxCuis.contains($0.drugARxCui.lowercased())
                && activeRxCuis.contains($0.drugBRxCui.lowercased())
        }
    }
}
