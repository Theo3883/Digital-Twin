import Foundation

struct DrugSearchResult: Codable, Identifiable {
    let rxCUI: String
    let name: String
    let synonym: String?

    var id: String { rxCUI }
}

