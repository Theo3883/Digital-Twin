import SwiftUI

/// Root overlay; SwiftUI equivalent of the former Blazor `OcrSheet` (removed with MAUI).
struct OcrSheetView: View {
    @ObservedObject var controller: OcrSessionController
    let repository: OcrRepository
    let onDismiss: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView(showsIndicators: false) {
                VStack(spacing: 16) {
                    switch controller.sheetPage {
                    case .scan:
                        ScanPageView(
                            controller: controller,
                            repository: repository,
                            onDismiss: onDismiss,
                            onGoToImport: { controller.goToImportPage() }
                        )
                    case .importOnly:
                        ImportPageView(
                            controller: controller,
                            repository: repository,
                            onBack: { controller.backToScanPage() },
                            onDismiss: onDismiss
                        )
                    case .result:
                        OcrResultPageView(
                            controller: controller,
                            repository: repository,
                            onDone: {
                                controller.completeSessionAndDismiss()
                                onDismiss()
                            }
                        )
                    }
                }
                .padding(16)
            }
            .navigationTitle(controller.sheetPage == .result ? "Result" : "Medical Documents")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Close") {
                        controller.resetForNewSession()
                        onDismiss()
                    }
                    .foregroundColor(.white.opacity(0.65))
                }
            }
        }
        .task {
            await controller.onOcrSheetAppear(repository: repository)
        }
        .sheet(isPresented: $controller.showScanner) {
            DocumentScannerView(
                onScanComplete: { controller.handleScannedImages($0, repository: repository) },
                onCancel: { controller.showScanner = false }
            )
        }
        .sheet(isPresented: $controller.showPhotoPicker) {
            PhotoLibraryPicker(
                onImagePicked: { img, data in controller.handlePickedPhoto(img, data, repository: repository) },
                onCancel: { controller.showPhotoPicker = false }
            )
        }
        .sheet(isPresented: $controller.showFilePicker) {
            FileImportPicker(
                onFilePicked: { controller.handleImportedFile($0, repository: repository) },
                onCancel: { controller.showFilePicker = false },
                onError: { msg in
                    controller.showFilePicker = false
                    controller.error = msg
                    controller.currentStep = .failed
                }
            )
        }
    }
}
