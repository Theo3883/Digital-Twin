import Foundation
import UIKit

protocol OcrRepository: Sendable {
    func loadDocuments() async -> [OcrDocumentInfo]
    func processFullOcr(_ combinedText: String) async -> OcrProcessingResult?
    func processFullOcrRawJson(_ combinedText: String) async -> String?
    func saveDocument(_ input: SaveOcrDocumentInput) async -> OcrDocumentInfo?
    func refreshAfterSave() async

    // Advanced OCR — Vault
    func vaultInitialize(_ input: VaultInitInput) async -> VaultResultInfo?
    func vaultUnlock(masterKeyBase64: String) async -> VaultResultInfo?
    func vaultLock() async -> VaultResultInfo?
    func vaultStoreDocument(_ input: VaultStoreDocumentInput) async -> VaultResultInfo?
    func vaultRetrieveDocument(documentId: String) async -> String?
    func vaultDeleteDocument(documentId: String) async -> VaultResultInfo?

    // Advanced OCR — Classification & Structured
    func classifyWithOrchestrator(ocrText: String, mlType: String?, mlConfidence: Float) async -> ClassificationResultInfo?
    func buildStructuredDocument(ocrText: String, docType: String, classConfidence: Float, classMethod: String) async -> StructuredMedicalDocumentInfo?
    func buildStructuredDocumentFromJson(_ input: BuildStructuredDocumentInput) async -> StructuredMedicalDocumentInfo?
    func validateDocument(headerBase64: String, fileExtension: String, fileSizeBytes: Int64) async -> VaultResultInfo?
}
