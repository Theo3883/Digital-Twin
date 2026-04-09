import Foundation

struct DiscontinueMedicationUseCase: Sendable {
    private let repository: MedicationRepository

    init(repository: MedicationRepository) {
        self.repository = repository
    }

    func callAsFunction(id: UUID, reason: String?) async -> Bool {
        await repository.discontinueMedication(id: id, reason: reason)
    }
}

