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

    func processFullOcrRawJson(_ combinedText: String) async -> String? {
        await engine.processFullOcrRawJson(combinedText)
    }

    func saveDocument(_ input: SaveOcrDocumentInput) async -> OcrDocumentInfo? {
        await engine.saveOcrDocument(input)
    }

    func refreshAfterSave() async {
        await engine.loadOcrDocuments()
        await engine.loadMedicalHistory()
    }

    // MARK: - Advanced OCR — Vault

    func vaultInitialize(_ input: VaultInitInput) async -> VaultResultInfo? {
        await engine.vaultInitialize(input)
    }

    func vaultUnlock(masterKeyBase64: String) async -> VaultResultInfo? {
        await engine.vaultUnlock(masterKeyBase64: masterKeyBase64)
    }

    func vaultLock() async -> VaultResultInfo? {
        await engine.vaultLock()
    }

    func vaultStoreDocument(_ input: VaultStoreDocumentInput) async -> VaultResultInfo? {
        await engine.vaultStoreDocument(input)
    }

    func vaultRetrieveDocument(documentId: String) async -> String? {
        await engine.vaultRetrieveDocument(documentId: documentId)
    }

    func vaultDeleteDocument(documentId: String) async -> VaultResultInfo? {
        await engine.vaultDeleteDocument(documentId: documentId)
    }

    // MARK: - Advanced OCR — Classification & Structured

    func classifyWithOrchestrator(ocrText: String, mlType: String?, mlConfidence: Float) async -> ClassificationResultInfo? {
        await engine.classifyWithOrchestrator(ocrText: ocrText, mlType: mlType, mlConfidence: mlConfidence)
    }

    func buildStructuredDocument(ocrText: String, docType: String, classConfidence: Float, classMethod: String) async -> StructuredMedicalDocumentInfo? {
        await engine.buildStructuredDocument(ocrText: ocrText, docType: docType, classConfidence: classConfidence, classMethod: classMethod)
    }

    func buildStructuredDocumentFromJson(_ input: BuildStructuredDocumentInput) async -> StructuredMedicalDocumentInfo? {
        await engine.buildStructuredDocumentFromJson(input)
    }

    func validateDocument(headerBase64: String, fileExtension: String, fileSizeBytes: Int64) async -> VaultResultInfo? {
        await engine.validateDocument(headerBase64: headerBase64, fileExtension: fileExtension, fileSizeBytes: fileSizeBytes)
    }
}
