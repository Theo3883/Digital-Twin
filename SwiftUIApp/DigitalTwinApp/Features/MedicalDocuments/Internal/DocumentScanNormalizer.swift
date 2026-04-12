import PDFKit
import UIKit

enum DocumentScanNormalizer {
    static func pdfDataFromScannedPages(_ images: [UIImage], jpegQuality: CGFloat = 0.92) -> Data? {
        guard !images.isEmpty else { return nil }
        let doc = PDFDocument()
        for (index, image) in images.enumerated() {
            guard let page = PDFPage(image: image) else { return nil }
            doc.insert(page, at: index)
        }
        return doc.dataRepresentation()
    }

    static func jpegData(_ image: UIImage, quality: CGFloat = 0.92) -> Data? {
        image.jpegData(compressionQuality: quality)
    }

    static func extractText(fromPdfData data: Data) -> String {
        guard let doc = PDFDocument(data: data) else { return "" }
        var parts: [String] = []
        for i in 0..<doc.pageCount {
            guard let page = doc.page(at: i) else { continue }
            if let s = page.string, !s.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                parts.append(s)
            }
        }
        return parts.joined(separator: "\n---\n")
    }
}
