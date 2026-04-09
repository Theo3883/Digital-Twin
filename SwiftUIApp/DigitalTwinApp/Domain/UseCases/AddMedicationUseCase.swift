import Foundation

struct AddMedicationUseCase: Sendable {
    private let repository: MedicationRepository

    init(repository: MedicationRepository) {
        self.repository = repository
    }

    func callAsFunction(_ input: AddMedicationInput) async -> Bool {
        await repository.addMedication(input)
    }
}

