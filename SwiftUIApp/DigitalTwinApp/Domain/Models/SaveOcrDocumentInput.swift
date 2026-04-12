import Foundation

/// Matches <see cref="DigitalTwin.Mobile.Application.DTOs.SaveOcrDocumentInput"/> (camelCase JSON).
struct SaveOcrDocumentInput: Codable {
    var documentId: String?
    var opaqueInternalName: String
    var mimeType: String
    var pageCount: Int
    var pageTexts: [String]
    var encryptedVaultPath: String?
    var sha256OfNormalized: String?
    var vaultOpaqueInternalName: String?
    var documentTypeOverride: String?
    /// Raw JSON string from `processFullOcr` (same payload the engine serialized).
    var cachedProcessingResultJson: String?
}
