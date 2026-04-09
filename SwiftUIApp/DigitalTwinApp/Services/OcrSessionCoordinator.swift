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
    func handleScannedImages(_ images: [UIImage], repository: OcrRepository) {
        scannedImages = images
        showScanner = false
        Task {
            await processImages(images, repository: repository)
        }
    }

    /// Handle image selected from photo library.
    func handlePickedImage(_ image: UIImage, repository: OcrRepository) {
        scannedImages = [image]
        showPhotoPicker = false
        Task {
            await processImages([image], repository: repository)
        }
    }

    /// Process images: Vision OCR → .NET pipeline → save document.
    func processImages(_ images: [UIImage], repository: OcrRepository) async {
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
            guard let result = await repository.processFullOcr(combinedText) else {
                error = "OCR pipeline failed to return a result"
                return
            }
            processingResult = result

            // Step 3: Save document with extracted data
            let fileName = "scan_\(Int(Date().timeIntervalSince1970))"
            await repository.saveDocument(
                opaqueInternalName: fileName,
                mimeType: "image/jpeg",
                pageCount: images.count,
                pageTexts: pageTexts
            )
            await repository.refreshAfterSave()
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
