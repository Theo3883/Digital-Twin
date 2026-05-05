import Foundation

struct SearchDrugsUseCase: Sendable {
    private let repository: MedicationRepository

    init(repository: MedicationRepository) {
        self.repository = repository
    }

    func callAsFunction(query: String) async -> [DrugSearchResult] {
        await repository.searchDrugs(query: query)
    }
}

