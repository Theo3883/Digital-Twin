import Foundation

protocol MedicationRepository: Sendable {
    func loadMedications() async -> [MedicationInfo]
    func searchDrugs(query: String) async -> [DrugSearchResult]
    func addMedication(_ input: AddMedicationInput) async -> OperationResult
    func discontinueMedication(id: UUID, reason: String?) async -> Bool
    func checkInteractions(rxCuis: [String]) async -> [MedicationInteractionInfo]
}

