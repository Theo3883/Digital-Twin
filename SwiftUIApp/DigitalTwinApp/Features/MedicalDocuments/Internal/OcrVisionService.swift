import UIKit
import Vision

@MainActor
final class OcrVisionService: ObservableObject {
    @Published var isProcessing = false
    @Published var lastError: String?

    func recognizeText(from image: UIImage) async throws -> String {
        guard let cgImage = image.cgImage else {
            throw OcrError.invalidImage
        }

        isProcessing = true
        defer { isProcessing = false }

        return try await withCheckedThrowingContinuation { continuation in
            let request = VNRecognizeTextRequest { request, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }

                guard let observations = request.results as? [VNRecognizedTextObservation] else {
                    continuation.resume(returning: "")
                    return
                }

                let text = observations.compactMap { observation in
                    observation.topCandidates(1).first?.string
                }.joined(separator: "\n")

                continuation.resume(returning: text)
            }

            request.recognitionLevel = .accurate
            request.usesLanguageCorrection = true
            request.recognitionLanguages = ["ro-RO", "en-US"]

            let handler = VNImageRequestHandler(cgImage: cgImage, options: [:])
            do {
                try handler.perform([request])
            } catch {
                continuation.resume(throwing: error)
            }
        }
    }

    func recognizePages(_ images: [UIImage]) async throws -> [String] {
        var pageTexts: [String] = []
        for image in images {
            let text = try await recognizeText(from: image)
            pageTexts.append(text)
        }
        return pageTexts
    }
}

enum OcrError: LocalizedError {
    case invalidImage

    var errorDescription: String? {
        switch self {
        case .invalidImage:
            return "Could not process the image"
        }
    }
}
