import Foundation

@MainActor
final class EngineOcrRepository: OcrRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadDocuments() async -> [OcrDocumentInfo] {
        await engine.loadOcrDocuments()
        return engine.ocrDocuments
    }

    func processFullOcr(_ combinedText: String) async -> OcrProcessingResult? {
        await engine.processFullOcr(combinedText)
    }

    func saveDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async {
        await engine.saveOcrDocument(
            opaqueInternalName: opaqueInternalName,
            mimeType: mimeType,
            pageCount: pageCount,
            pageTexts: pageTexts
        )
    }

    func refreshAfterSave() async {
        await engine.loadOcrDocuments()
        await engine.loadMedicalHistory()
    }
}

