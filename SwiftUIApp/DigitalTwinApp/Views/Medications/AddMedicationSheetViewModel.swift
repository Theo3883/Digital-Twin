import Foundation

@MainActor
final class AddMedicationSheetViewModel: ObservableObject {
    @Published private(set) var searchResults: [DrugSearchResult] = []

    private let searchDrugs: SearchDrugsUseCase
    private let addMedication: AddMedicationUseCase

    init(searchDrugs: SearchDrugsUseCase, addMedication: AddMedicationUseCase) {
        self.searchDrugs = searchDrugs
        self.addMedication = addMedication
    }

    func search(query: String) async {
        guard query.count >= 3 else {
            searchResults = []
            return
        }
        searchResults = await searchDrugs(query: query)
    }

    func clearResults() {
        searchResults = []
    }

    func add(input: AddMedicationInput) async -> Bool {
        await addMedication(input)
    }
}

