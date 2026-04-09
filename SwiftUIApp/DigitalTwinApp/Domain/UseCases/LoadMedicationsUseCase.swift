import Foundation

struct LoadMedicationsUseCase: Sendable {
    private let repository: MedicationRepository

    init(repository: MedicationRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> [MedicationInfo] {
        await repository.loadMedications()
    }
}

