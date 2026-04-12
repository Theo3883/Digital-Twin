import SwiftUI

/// Mirrors [`ImportPage.razor`](DigitalTwin.OCR/Components/Pages/ImportPage.razor).
struct ImportPageView: View {
    @ObservedObject var controller: OcrSessionController
    let repository: OcrRepository
    let onBack: () -> Void
    let onDismiss: () -> Void

    private var posture: OcrSecurityPosture { controller.currentPosture() }
    private var securityReady: Bool { DocumentSecurityPolicy.requiredOcrRowsSatisfied(posture) }

    var body: some View {
        VStack(spacing: 16) {
            SecurityPostureCardView(
                posture: posture,
                onUnlock: { Task { await controller.unlockVault(repository: repository) } },
                onInitVault: { Task { await controller.initializeVault(repository: repository) } }
            )

            VStack(spacing: 16) {
                Text("Import Document")
                    .font(.headline)
                    .foregroundColor(.white)
                Text("Choose a photo from your library, or pick a PDF / image from Files (max 50 MB).")
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.55))
                    .multilineTextAlignment(.center)

                if controller.isLoadingVault {
                    ProgressView().tint(.white)
                    Text(controller.statusMessage ?? "")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.55))
                } else if let err = controller.error, controller.currentStep == .failed, !controller.isProcessing {
                    Text(err)
                        .font(.caption)
                        .foregroundColor(LiquidGlass.redCritical)
                    Button("Try Again") {
                        controller.error = nil
                        controller.currentStep = .idle
                    }
                } else if !securityReady {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Image(systemName: "shield.lefthalf.filled.badge.checkmark")
                            Text("Complete security checks first")
                                .font(.subheadline.weight(.semibold))
                        }
                        Text("Go back and fix passcode, vault initialization, and vault unlock on the previous screen. Face ID is optional.")
                            .font(.caption)
                    }
                    .foregroundColor(LiquidGlass.redCritical.opacity(0.9))
                    .padding(14)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(LiquidGlass.redCritical.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 12))

                    Button("← Back", action: onBack)
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                } else if controller.isProcessing {
                    ProgressView().tint(.white)
                    Text(controller.currentStep.label)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.55))
                } else {
                    Button {
                        controller.startPhotoPick()
                    } label: {
                        HStack {
                            Image(systemName: "photo.fill")
                            Text("Choose from Photos")
                        }
                        .font(.subheadline.weight(.semibold))
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 12)
                    }
                    .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.4))

                    Button {
                        controller.startFileImport()
                    } label: {
                        HStack {
                            Image(systemName: "folder.fill")
                            Text("Choose from Files")
                        }
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 12)
                    }
                    .glassPill(tint: Color.white.opacity(0.12))

                    Button("← Back", action: onBack)
                        .font(.subheadline)
                        .foregroundColor(.white.opacity(0.65))
                }
            }
            .padding(24)
            .glassCard()

            Button("Cancel", action: onDismiss)
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.5))
        }
    }
}
