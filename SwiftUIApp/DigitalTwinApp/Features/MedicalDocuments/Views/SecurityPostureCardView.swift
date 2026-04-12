import SwiftUI
import UIKit

/// Mirrors [`SecurityPostureCard.razor`](DigitalTwin.OCR/Components/Shared/SecurityPostureCard.razor).
struct SecurityPostureCardView: View {
    let posture: OcrSecurityPosture
    let onUnlock: () -> Void
    let onInitVault: () -> Void

    private var overallOK: Bool {
        DocumentSecurityPolicy.requiredOcrRowsSatisfied(posture)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 8) {
                Image(systemName: "lock.shield.fill")
                    .foregroundColor(overallOK ? LiquidGlass.greenPositive : LiquidGlass.amberWarning)
                Text("Security")
                    .font(.subheadline.weight(.semibold))
                    .foregroundColor(.white)
                Spacer()
            }

            PostureRowView(label: "Passcode", isOk: posture.isPasscodeSet, systemImage: "number")
            PostureRowView(
                label: biometryTitle,
                isOk: posture.isBiometryAvailable,
                isOptional: true,
                systemImage: "touchid"
            )
            PostureRowView(label: "Vault Initialized", isOk: posture.isVaultInitialized, systemImage: "lock.fill")
            PostureRowView(label: "Vault Unlocked", isOk: posture.isVaultUnlocked, systemImage: "lock.open.fill")

            if !posture.isPasscodeSet {
                Text("A device passcode is required. Set one in iOS Settings → Face ID & Passcode.")
                    .font(.caption2)
                    .foregroundColor(LiquidGlass.amberWarning)
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(LiquidGlass.amberWarning.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 12))

                Button("Open Settings") {
                    if let url = URL(string: UIApplication.openSettingsURLString) {
                        UIApplication.shared.open(url)
                    }
                }
                .font(.caption.weight(.semibold))
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 10)
                .glassPill(tint: LiquidGlass.amberWarning.opacity(0.25))
            }

            if !posture.isBiometryAvailable && posture.isPasscodeSet {
                Text("Face ID / Touch ID unavailable — passcode will be used instead.")
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
                    .padding(8)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(Color.white.opacity(0.04))
                    .clipShape(RoundedRectangle(cornerRadius: 10))
            }

            if !posture.isVaultInitialized {
                Button(action: onInitVault) {
                    HStack {
                        Image(systemName: "lock.fill")
                        Text("Initialize Secure Vault")
                    }
                    .font(.subheadline.weight(.semibold))
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 12)
                }
                .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.35))
            } else if !posture.isVaultUnlocked {
                Button(action: onUnlock) {
                    HStack {
                        Image(systemName: "touchid")
                        Text("UNLOCK VAULT")
                            .fontWeight(.bold)
                    }
                    .font(.subheadline)
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 14)
                }
                .glassPill(tint: LiquidGlass.tealPrimary)
            }
        }
        .padding(16)
        .glassCard()
    }

    private var biometryTitle: String {
        if posture.biometryTypeLabel == "None" || posture.biometryTypeLabel.isEmpty {
            return "Biometrics"
        }
        return posture.biometryTypeLabel
    }
}
