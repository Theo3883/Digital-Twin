import Foundation

struct IdentityValidationInfo: Codable {
    let isValid: Bool
    let nameMatched: Bool
    let cnpMatched: Bool
    let reason: String?
}

