import Foundation

struct VaultResultInfo: Codable {
    let success: Bool
    let error: String?
    let documentId: String?
    let vaultPath: String?
    let sha256: String?
    let opaqueInternalName: String?
}
