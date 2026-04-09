import SwiftUI
import LocalAuthentication

struct OcrDocumentView: View {
    @StateObject private var coordinator = OcrSessionCoordinator()
    @StateObject private var viewModel: OcrDocumentsViewModel
    @State private var unlockedDocId: UUID?

    private let repository: OcrRepository

    init(viewModel: OcrDocumentsViewModel, repository: OcrRepository) {
        _viewModel = StateObject(wrappedValue: viewModel)
        self.repository = repository
    }

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
                if viewModel.documents.isEmpty {
                    EmptyDocumentsView()
                } else {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("Uploaded Documents · \(viewModel.documents.count)")
                            .font(.subheadline.weight(.medium))
                            .foregroundColor(.white.opacity(0.65))
                            .padding(.horizontal, 4)

                        ForEach(viewModel.documents) { doc in
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
            await viewModel.load()
        }
        .refreshable {
            await viewModel.load()
        }
        .sheet(isPresented: $coordinator.showScanner) {
            DocumentScannerView(
                onScanComplete: { images in
                    coordinator.handleScannedImages(images, repository: repository)
                    Task { await viewModel.load() }
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
