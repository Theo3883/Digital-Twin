import Foundation

struct VaultStoreDocumentInput: Codable {
    let documentBase64: String
    let mimeType: String
    let pageCount: Int
    let documentId: String
}
