import SwiftUI
import UIKit
import PhotosUI

/// Coordinates the full OCR session: scan/pick → Vision OCR → .NET processing → save.
@MainActor
final class OcrSessionCoordinator: ObservableObject {
    @Published var isScanning = false
    @Published var isProcessing = false
    @Published var scannedImages: [UIImage] = []
    @Published var processingResult: OcrProcessingResult?
    @Published var error: String?
    @Published var showScanner = false
    @Published var showPhotoPicker = false

    private let visionService = OcrVisionService()

    /// Start a document camera scan session.
    func startScan() {
        scannedImages = []
        processingResult = nil
        error = nil
        showScanner = true
    }

    /// Handle scanned images from the document camera.
    func handleScannedImages(_ images: [UIImage], engine: MobileEngineWrapper) {
        scannedImages = images
        showScanner = false
        Task {
            await processImages(images, engine: engine)
        }
    }

    /// Handle image selected from photo library.
    func handlePickedImage(_ image: UIImage, engine: MobileEngineWrapper) {
        scannedImages = [image]
        showPhotoPicker = false
        Task {
            await processImages([image], engine: engine)
        }
    }

    /// Process images: Vision OCR → .NET pipeline → save document.
    func processImages(_ images: [UIImage], engine: MobileEngineWrapper) async {
        isProcessing = true
        error = nil
        defer { isProcessing = false }

        do {
            // Step 1: Vision OCR on all pages
            let pageTexts = try await visionService.recognizePages(images)

            guard !pageTexts.allSatisfy({ $0.isEmpty }) else {
                error = "No text recognized in the scanned document"
                return
            }

            // Step 2: Process full OCR through .NET engine
            let combinedText = pageTexts.joined(separator: "\n---\n")
            let result = await engine.processFullOcr(combinedText)
            processingResult = result

            // Step 3: Save document with extracted data
            let fileName = "scan_\(Int(Date().timeIntervalSince1970))"
            await engine.saveOcrDocument(
                opaqueInternalName: fileName,
                mimeType: "image/jpeg",
                pageCount: images.count,
                pageTexts: pageTexts
            )

            // Refresh OCR documents list
            await engine.loadOcrDocuments()
            await engine.loadMedicalHistory()
        } catch {
            self.error = error.localizedDescription
        }
    }

    func handleScannerCancel() {
        showScanner = false
    }

    func reset() {
        scannedImages = []
        processingResult = nil
        error = nil
        isScanning = false
        isProcessing = false
    }
}
