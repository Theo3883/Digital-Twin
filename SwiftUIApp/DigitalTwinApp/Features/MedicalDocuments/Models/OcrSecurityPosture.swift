import Foundation

/// Mirrors `DigitalTwin.Mobile.OCR.Models.SecurityPosture` record.
struct OcrSecurityPosture: Sendable {
    var isPasscodeSet: Bool
    var isBiometryAvailable: Bool
    var biometryTypeLabel: String
    var isVaultInitialized: Bool
    /// UI + policy: vault unlocked for this OCR session (may be false while keychain exists).
    var isVaultUnlocked: Bool
    var activeMode: OcrSecurityMode
}
