import Foundation

struct DrugSearchResult: Codable, Identifiable {
    let rxCui: String
    let name: String

    var id: String { rxCui }
}

