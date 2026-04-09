import Foundation

struct DocumentIdentityInfo: Codable {
    let extractedName: String?
    let extractedCnp: String?
    let nameConfidence: Float
    let cnpConfidence: Float
}

