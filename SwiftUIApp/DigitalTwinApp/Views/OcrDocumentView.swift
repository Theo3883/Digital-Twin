import SwiftUI
import LocalAuthentication

struct OcrDocumentView: View {
    @EnvironmentObject var engineWrapper: MobileEngineWrapper
    @StateObject private var coordinator = OcrSessionCoordinator()
    @State private var showingScanner = false
    @State private var unlockedDocId: UUID?

    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                // Action Buttons
                HStack(spacing: 12) {
                    Button {
                        coordinator.startScan()
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "doc.text.viewfinder")
                                .font(.system(size: 14))
                            Text("Scan")
                                .font(.caption.weight(.medium))
                        }
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 12)
                    }
                    .glassPill(tint: LiquidGlass.tealPrimary.opacity(0.2))

                    Button {
                        coordinator.showPhotoPicker = true
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "photo.on.rectangle")
                                .font(.system(size: 14))
                            Text("Import")
                                .font(.caption.weight(.medium))
                        }
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 12)
                    }
                    .glassPill(tint: .white.opacity(0.05))
                }

                // Processing indicator
                if coordinator.isProcessing {
                    HStack(spacing: 8) {
                        ProgressView()
                            .tint(.white)
                        Text("Processing document…")
                            .font(.subheadline).foregroundColor(.white.opacity(0.65))
                    }
                    .frame(maxWidth: .infinity)
                    .glassCard()
                }

                // Error banner
                if let error = coordinator.error {
                    HStack {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(LiquidGlass.amberWarning)
                        Text(error)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.8))
                    }
                    .glassBanner(tint: LiquidGlass.amberWarning.opacity(0.2))
                }

                // Processing result
                if let result = coordinator.processingResult {
                    OcrResultCard(result: result)
                }

                // Document List
                if engineWrapper.ocrDocuments.isEmpty {
                    EmptyDocumentsView()
                } else {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("Uploaded Documents · \(engineWrapper.ocrDocuments.count)")
                            .font(.subheadline.weight(.medium))
                            .foregroundColor(.white.opacity(0.65))
                            .padding(.horizontal, 4)

                        ForEach(engineWrapper.ocrDocuments) { doc in
                            OcrDocumentCard(
                                document: doc,
                                isUnlocked: unlockedDocId == doc.id,
                                onUnlock: { authenticateAndUnlock(doc) }
                            )
                        }
                    }
                }

                Spacer(minLength: 100)
            }
            .padding(16)
        }
        .pageEnterAnimation()
        .task {
            await engineWrapper.loadOcrDocuments()
        }
        .refreshable {
            await engineWrapper.loadOcrDocuments()
        }
        .sheet(isPresented: $coordinator.showScanner) {
            DocumentScannerView(
                onScanComplete: { images in
                    coordinator.handleScannedImages(images, engine: engineWrapper)
                },
                onCancel: {
                    coordinator.handleScannerCancel()
                }
            )
        }
    }

    private func authenticateAndUnlock(_ doc: OcrDocumentInfo) {
        let context = LAContext()
        var error: NSError?
        guard context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error) else {
            unlockedDocId = doc.id // Fallback: unlock without biometrics
            return
        }
        context.evaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, localizedReason: "Unlock document") { success, _ in
            DispatchQueue.main.async {
                if success { unlockedDocId = doc.id }
            }
        }
    }
}

// MARK: - OCR Result Card

struct OcrResultCard: View {
    let result: OcrProcessingResult

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(LiquidGlass.greenPositive)
                Text("Document Processed")
                    .font(.subheadline.weight(.semibold))
                    .foregroundColor(.white)
                Spacer()
                Text(result.documentType)
                    .font(.system(size: 10, weight: .medium))
                    .foregroundColor(LiquidGlass.tealPrimary)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(LiquidGlass.tealPrimary.opacity(0.15)))
            }

            if let identity = result.identity {
                if let name = identity.extractedName {
                    Label(name, systemImage: "person.fill")
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.7))
                }
                if let cnp = identity.extractedCnp {
                    Label("CNP: \(cnp.prefix(4))****", systemImage: "number")
                        .font(.caption2).foregroundColor(.white.opacity(0.4))
                }
            }

            if let validation = result.validation {
                HStack(spacing: 6) {
                    Image(systemName: validation.isValid ? "checkmark.shield.fill" : "xmark.shield.fill")
                        .foregroundColor(validation.isValid ? LiquidGlass.greenPositive : LiquidGlass.redCritical)
                    Text(validation.isValid ? "Identity verified" : (validation.reason ?? "Identity mismatch"))
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.65))
                }
            }

            if !result.historyItems.isEmpty {
                Text("\(result.historyItems.count) medication(s) extracted")
                    .font(.caption).foregroundColor(.white.opacity(0.4))
            }
        }
        .glassCard()
    }
}

// MARK: - Document Card

struct OcrDocumentCard: View {
    let document: OcrDocumentInfo
    let isUnlocked: Bool
    let onUnlock: () -> Void

    private var mimeLabel: String {
        if let name = document.opaqueInternalName?.lowercased() {
            if name.hasSuffix(".pdf") { return "PDF" }
            if name.hasSuffix(".jpg") || name.hasSuffix(".jpeg") { return "JPG" }
            if name.hasSuffix(".png") { return "PNG" }
        }
        return "IMG"
    }

    private var mimeColor: Color {
        switch mimeLabel {
        case "PDF": return LiquidGlass.redCritical
        case "JPG", "JPEG": return LiquidGlass.amberWarning
        case "PNG": return .blue
        default: return .gray
        }
    }

    var body: some View {
        HStack(spacing: 12) {
            // MIME type badge
            Text(mimeLabel)
                .font(.system(size: 10, weight: .bold))
                .foregroundColor(mimeColor)
                .frame(width: 40, height: 40)
                .background {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(mimeColor.opacity(0.15))
                }

            VStack(alignment: .leading, spacing: 4) {
                Text(document.opaqueInternalName ?? "Document")
                    .font(.system(size: 14, weight: .medium))
                    .foregroundColor(.white)
                    .lineLimit(1)
                HStack(spacing: 8) {
                    Text(document.scannedAt.formatted(date: .abbreviated, time: .shortened))
                        .font(.caption2).foregroundColor(.white.opacity(0.4))
                    Text("·")
                        .foregroundColor(.white.opacity(0.2))
                    Text("\(document.pageCount) pg")
                        .font(.caption2).foregroundColor(.white.opacity(0.4))
                }
            }

            Spacer()

            if isUnlocked {
                Image(systemName: "lock.open.fill")
                    .font(.caption)
                    .foregroundColor(LiquidGlass.greenPositive)
            } else {
                Button(action: onUnlock) {
                    HStack(spacing: 4) {
                        Image(systemName: "faceid")
                            .font(.system(size: 12))
                        Text("Unlock")
                            .font(.caption2.weight(.medium))
                    }
                    .foregroundColor(LiquidGlass.tealPrimary)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background {
                        RoundedRectangle(cornerRadius: LiquidGlass.radiusChip)
                            .stroke(LiquidGlass.tealPrimary.opacity(0.4), lineWidth: 1)
                    }
                }
            }
        }
        .glassCard()
    }
}

// MARK: - Empty State

struct EmptyDocumentsView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "doc.text.fill")
                .font(.system(size: 50))
                .foregroundColor(.white.opacity(0.3))
            Text("No Documents")
                .font(.title3).fontWeight(.semibold)
                .foregroundColor(.white)
            Text("Scan or import medical documents to extract and organize your health records.")
                .font(.subheadline).foregroundColor(.white.opacity(0.65))
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 60)
    }
}
