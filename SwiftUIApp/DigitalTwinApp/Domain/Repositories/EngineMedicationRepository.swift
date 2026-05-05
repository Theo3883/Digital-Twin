import Foundation

@MainActor
final class EngineMedicationRepository: MedicationRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadMedications() async -> [MedicationInfo] {
        await engine.loadMedications()
        return engine.medications
    }

    func searchDrugs(query: String) async -> [DrugSearchResult] {
        await engine.searchDrugs(query: query)
    }

    func addMedication(_ input: AddMedicationInput) async -> OperationResult {
        await engine.addMedication(input)
    }

    func discontinueMedication(id: UUID, reason: String?) async -> Bool {
        await engine.discontinueMedication(id: id, reason: reason)
    }

    func checkInteractions(rxCuis: [String]) async -> [MedicationInteractionInfo] {
        await engine.checkInteractions(rxCuis: rxCuis)
    }
}

