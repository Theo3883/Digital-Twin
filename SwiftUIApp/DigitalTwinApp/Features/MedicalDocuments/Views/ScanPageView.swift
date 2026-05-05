import SwiftUI

/// SwiftUI equivalent of the former Blazor `ScanPage` (removed with MAUI).
struct ScanPageView: View {
    @ObservedObject var controller: OcrSessionController
    let repository: OcrRepository
    let onDismiss: () -> Void
    let onGoToImport: () -> Void

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
                Text("Scan Medical Document")
                    .font(.headline)
                    .foregroundColor(.white)
                Text("Camera scan, pick from Photos, or import PDF / images from Files.")
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.55))
                    .multilineTextAlignment(.center)

                if controller.isLoadingVault {
                    ProgressView()
                        .tint(.white)
                    Text(controller.statusMessage ?? "")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.55))
                } else if let err = controller.error, controller.currentStep == .failed, !controller.isProcessing {
                    Text(err)
                        .font(.caption)
                        .foregroundColor(LiquidGlass.redCritical)
                        .padding(12)
                        .frame(maxWidth: .infinity)
                        .background(LiquidGlass.redCritical.opacity(0.12))
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                    Button("Try Again") {
                        controller.error = nil
                        controller.currentStep = .idle
                    }
                    .font(.subheadline)
                    .foregroundColor(.white.opacity(0.8))
                } else if !securityReady {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Image(systemName: "shield.lefthalf.filled.badge.checkmark")
                            Text("Complete security checks first")
                                .font(.subheadline.weight(.semibold))
                        }
                        .foregroundColor(LiquidGlass.redCritical.opacity(0.95))
                        Text("Fix every required item above (passcode, vault initialized, vault unlocked). Face ID is optional.")
                            .font(.caption)
                            .foregroundColor(LiquidGlass.redCritical.opacity(0.85))
                    }
                    .padding(14)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(LiquidGlass.redCritical.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                } else if controller.isProcessing {
                    ProgressView()
                        .tint(.white)
                    Text(controller.currentStep.label)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.55))
                } else {
                    HStack(spacing: 10) {
                        scanButton(icon: "camera.fill", title: "Camera") {
                            controller.startScan()
                        }
                        scanButton(icon: "photo.fill", title: "Photos") {
                            controller.startPhotoPick()
                        }
                    }
                    Button {
                        controller.startFileImport()
                    } label: {
                        HStack {
                            Image(systemName: "folder.fill")
                            Text("Files")
                        }
                        .font(.subheadline.weight(.medium))
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 12)
                    }
                    .glassPill(tint: Color.white.opacity(0.12))

                    Button("More import options…", action: onGoToImport)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.45))
                }
            }
            .padding(24)
            .glassCard()

            Button("Cancel", action: onDismiss)
                .font(.subheadline)
                .foregroundColor(.white.opacity(0.5))
        }
    }

    private func scanButton(icon: String, title: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(spacing: 6) {
                Image(systemName: icon)
                Text(title)
                    .font(.caption.weight(.medium))
            }
            .foregroundColor(.white)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 12)
        }
        .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.35))
    }
}
