import LocalAuthentication
import SwiftUI

/// Medical documents hub — restored Scan / Import layout with security card + inline result (legacy UI parity).
struct OcrDocumentRootView: View {
    @StateObject private var sessionController = OcrSessionController()
    @StateObject private var listViewModel: OcrDocumentsViewModel
    @State private var unlockedDocId: UUID?
    @State private var showImportSourcePicker = false

    private let repository: OcrRepository

    init(viewModel: OcrDocumentsViewModel, repository: OcrRepository) {
        _listViewModel = StateObject(wrappedValue: viewModel)
        self.repository = repository
    }

    private var posture: OcrSecurityPosture { sessionController.currentPosture() }
    private var securityReady: Bool { DocumentSecurityPolicy.requiredOcrRowsSatisfied(posture) }

    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                SecurityPostureCardView(
                    posture: posture,
                    onUnlock: { Task { await sessionController.unlockVault(repository: repository) } },
                    onInitVault: { Task { await sessionController.initializeVault(repository: repository) } }
                )

                HStack(spacing: 12) {
                    Button {
                        guard securityReady else { return }
                        sessionController.startScan()
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
                    .opacity(securityReady ? 1 : 0.45)
                    .disabled(!securityReady)

                    Button {
                        guard securityReady else { return }
                        showImportSourcePicker = true
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
                    .opacity(securityReady ? 1 : 0.45)
                    .disabled(!securityReady)
                }

                if !securityReady {
                    HStack(spacing: 8) {
                        Image(systemName: "shield.lefthalf.filled.badge.checkmark")
                            .foregroundColor(LiquidGlass.redCritical.opacity(0.9))
                        Text("Complete security checks above, then unlock the vault.")
                            .font(.caption)
                            .foregroundColor(LiquidGlass.redCritical.opacity(0.9))
                    }
                    .padding(12)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(LiquidGlass.redCritical.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }

                if sessionController.isProcessing {
                    HStack(spacing: 8) {
                        ProgressView()
                            .tint(.white)
                        Text("Processing document…")
                            .font(.subheadline)
                            .foregroundColor(.white.opacity(0.65))
                    }
                    .frame(maxWidth: .infinity)
                    .glassCard()
                }

                if let error = sessionController.error {
                    HStack {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(LiquidGlass.amberWarning)
                        Text(error)
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.8))
                    }
                    .glassBanner(tint: LiquidGlass.amberWarning.opacity(0.2))
                }

                if let result = sessionController.processingResult {
                    OcrResultCard(result: result)
                }

                if listViewModel.documents.isEmpty {
                    EmptyDocumentsView()
                } else {
                    VStack(alignment: .leading, spacing: 12) {
                        Text("Uploaded Documents · \(listViewModel.documents.count)")
                            .font(.subheadline.weight(.medium))
                            .foregroundColor(.white.opacity(0.65))
                            .padding(.horizontal, 4)

                        ForEach(listViewModel.documents) { doc in
                            NavigationLink {
                                MedicalDocumentDetailView(document: doc, repository: repository)
                            } label: {
                                OcrDocumentCard(
                                    document: doc,
                                    isUnlocked: unlockedDocId == doc.id,
                                    onUnlock: { authenticateAndUnlock(doc) }
                                )
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }

                Spacer(minLength: 100)
            }
            .padding(16)
        }
        .pageEnterAnimation()
        .task {
            await sessionController.onOcrSheetAppear(repository: repository)
            await listViewModel.load()
        }
        .refreshable {
            await listViewModel.load()
        }
        .confirmationDialog("Import from", isPresented: $showImportSourcePicker, titleVisibility: .visible) {
            Button("Photo Library") {
                sessionController.startPhotoPick()
            }
            Button("Files") {
                sessionController.startFileImport()
            }
            Button("Cancel", role: .cancel) {}
        }
        .sheet(isPresented: $sessionController.showScanner) {
            DocumentScannerView(
                onScanComplete: { images in
                    sessionController.handleScannedImages(images, repository: repository)
                    Task { await listViewModel.load() }
                },
                onCancel: { sessionController.showScanner = false }
            )
        }
        .sheet(isPresented: $sessionController.showPhotoPicker) {
            PhotoLibraryPicker(
                onImagePicked: { img, data in sessionController.handlePickedPhoto(img, data, repository: repository) },
                onCancel: { sessionController.showPhotoPicker = false }
            )
        }
        .sheet(isPresented: $sessionController.showFilePicker) {
            FileImportPicker(
                onFilePicked: { sessionController.handleImportedFile($0, repository: repository) },
                onCancel: { sessionController.showFilePicker = false },
                onError: { msg in
                    sessionController.showFilePicker = false
                    sessionController.error = msg
                    sessionController.currentStep = .failed
                }
            )
        }
    }

    private func authenticateAndUnlock(_ doc: OcrDocumentInfo) {
        let context = LAContext()
        var error: NSError?
        guard context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error) else {
            unlockedDocId = doc.id
            return
        }
        context.evaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, localizedReason: "Unlock document") { success, _ in
            DispatchQueue.main.async {
                if success { unlockedDocId = doc.id }
            }
        }
    }
}
