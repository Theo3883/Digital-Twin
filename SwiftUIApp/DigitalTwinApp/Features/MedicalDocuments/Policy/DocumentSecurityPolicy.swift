import Foundation

/// Pure functions mirroring [`DocumentSecurityPolicy`](Mobile/DigitalTwin.Mobile.OCR/Policies/DocumentSecurityPolicy.cs).
enum DocumentSecurityPolicy {
    static func requiredOcrRowsSatisfied(_ posture: OcrSecurityPosture) -> Bool {
        if !posture.isVaultInitialized || !posture.isVaultUnlocked {
            return false
        }
        if posture.activeMode == .relaxedDebug {
            return true
        }
        return posture.isPasscodeSet
    }

    static func canInitializeVault(_ posture: OcrSecurityPosture) -> Bool {
        if posture.activeMode == .relaxedDebug {
            return true
        }
        return posture.isPasscodeSet
    }
}
