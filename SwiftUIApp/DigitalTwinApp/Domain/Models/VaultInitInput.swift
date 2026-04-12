import Foundation

struct VaultInitInput: Codable {
    let isPasscodeSet: Bool
    let isBiometryAvailable: Bool
    let biometryType: String
    let isVaultInitialized: Bool
    let isVaultUnlocked: Bool
    let activeMode: String
}
