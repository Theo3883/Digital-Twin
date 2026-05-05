import Foundation

struct CheckMedicationInteractionsUseCase: Sendable {
    private let repository: MedicationRepository

    init(repository: MedicationRepository) {
        self.repository = repository
    }

    func callAsFunction(rxCuis: [String]) async -> [MedicationInteractionInfo] {
        await repository.checkInteractions(rxCuis: rxCuis)
    }
}

