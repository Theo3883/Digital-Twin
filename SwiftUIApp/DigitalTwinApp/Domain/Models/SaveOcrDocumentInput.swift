import Foundation

struct SaveOcrDocumentInput: Codable {
    let opaqueInternalName: String
    let mimeType: String
    let pageCount: Int
    let pageTexts: [String]
}

