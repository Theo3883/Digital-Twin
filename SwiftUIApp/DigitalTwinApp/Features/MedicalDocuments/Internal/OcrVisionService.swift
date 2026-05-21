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

    /// Build a token-level OCR graph for the given pages. Bounding boxes are normalized (0..1)
    /// and Y is converted from Vision's bottom-left origin to top-left origin expected by the engine.
    func buildGraphForPages(_ images: [UIImage]) async throws -> OcrDocumentGraph {
        var allTokens: [OcrToken] = []
        var pages: [OcrGraphPage] = []
        var tokenIndex = 0

        for (pageIndex, image) in images.enumerated() {
            guard let cgImage = image.cgImage else { continue }

            let request = VNRecognizeTextRequest()
            request.recognitionLevel = .accurate
            request.usesLanguageCorrection = true
            request.recognitionLanguages = ["ro-RO", "en-US"]

            let handler = VNImageRequestHandler(cgImage: cgImage, options: [:])
            do {
                try handler.perform([request])
            } catch {
                continue
            }

            guard let observations = request.results as? [VNRecognizedTextObservation] else {
                pages.append(OcrGraphPage(pageIndex: pageIndex, pageWidth: 1.0, pageHeight: 1.0))
                continue
            }

            for obs in observations {
                guard let candidate = obs.topCandidates(1).first else { continue }
                let raw = candidate.string
                let tokens = raw.split{ $0.isWhitespace }.map { String($0) }
                guard !tokens.isEmpty else { continue }

                // Subdivide observation bounding box horizontally across tokens.
                let bb = obs.boundingBox // normalized, origin bottom-left
                let pieceWidth = bb.width / CGFloat(tokens.count)
                var x0 = bb.origin.x
                for t in tokens {
                    let cleaned = cleanTokenText(t)
                    let x = Float(x0)
                    let y = Float(1.0 - (bb.origin.y + bb.height)) // invert Y
                    let w = Float(pieceWidth)
                    let h = Float(bb.height)
                    let obb = OcrBoundingBox(x: x, y: y, width: w, height: h)
                    let token = OcrToken(tokenIndex: tokenIndex, text: cleaned, confidence: 1.0, boundingBox: obb, pageIndex: pageIndex, blockIndex: 0, lineIndex: 0, isBoundingBoxApproximate: false)
                    allTokens.append(token)
                    tokenIndex += 1
                    x0 += pieceWidth
                }
            }

            pages.append(OcrGraphPage(pageIndex: pageIndex, pageWidth: 1.0, pageHeight: 1.0))
        }

        return OcrDocumentGraph(allTokens: allTokens, pages: pages, detectedLanguage: nil)
    }

    private func cleanTokenText(_ s: String) -> String {
        // Remove pipe characters and common control characters, collapse whitespace
        var out = s.replacingOccurrences(of: "|", with: "")
        out = out.components(separatedBy: .controlCharacters).joined()
        out = out.components(separatedBy: .whitespacesAndNewlines).filter { !$0.isEmpty }.joined(separator: " ")
        return out
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
