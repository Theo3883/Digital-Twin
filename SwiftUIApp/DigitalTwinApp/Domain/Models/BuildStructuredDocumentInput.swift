import Foundation

struct BuildStructuredDocumentInput: Codable {
    var documentId: String
    var ocrText: String
    var docType: String
    var classConfidence: Float
    var classMethod: String
    var useMlExtraction: Bool
    var ocrDurationMs: Int64
    var classificationDurationMs: Int64
}
