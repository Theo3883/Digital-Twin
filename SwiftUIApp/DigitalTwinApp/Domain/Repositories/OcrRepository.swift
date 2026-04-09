import Foundation
import UIKit

protocol OcrRepository: Sendable {
    func loadDocuments() async -> [OcrDocumentInfo]
    func processFullOcr(_ combinedText: String) async -> OcrProcessingResult?
    func saveDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async
    func refreshAfterSave() async
}

