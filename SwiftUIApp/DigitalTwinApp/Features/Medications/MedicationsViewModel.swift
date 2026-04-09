import Foundation

@MainActor
final class MedicationsViewModel: ObservableObject {
    @Published private(set) var medications: [MedicationInfo] = []
    @Published private(set) var interactions: [MedicationInteractionInfo] = []
    @Published var drugSearchResults: [DrugSearchResult] = []

    @Published var isAddSheetPresented: Bool = false
    @Published var isInteractionsSheetPresented: Bool = false
    @Published var endReason: String = ""
    @Published var isEndReasonDialogPresented: Bool = false
    @Published var selectedMedication: MedicationInfo?

    private let loadMedications: LoadMedicationsUseCase
    private let checkInteractions: CheckMedicationInteractionsUseCase
    private let discontinue: DiscontinueMedicationUseCase

    init(loadMedications: LoadMedicationsUseCase, checkInteractions: CheckMedicationInteractionsUseCase, discontinue: DiscontinueMedicationUseCase) {
        self.loadMedications = loadMedications
        self.checkInteractions = checkInteractions
        self.discontinue = discontinue
    }

    var activeMedications: [MedicationInfo] {
        medications.filter { $0.isActive }
    }

    var inactiveMedications: [MedicationInfo] {
        medications.filter { !$0.isActive }
    }

    func refresh() async {
        medications = await loadMedications()
        await refreshInteractions()
    }

    func refreshInteractions() async {
        let rxCuis = medications
            .filter { $0.isActive && $0.rxCUI != nil }
            .compactMap { $0.rxCUI }

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
}

