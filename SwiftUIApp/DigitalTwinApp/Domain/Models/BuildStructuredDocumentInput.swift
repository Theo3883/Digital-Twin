import Foundation

// OCR graph types included here to ensure they are visible to the compiler
struct OcrBoundingBox: Codable {
    var x: Float
    var y: Float
    var width: Float
    var height: Float
}

struct OcrToken: Codable {
    var tokenIndex: Int
    var text: String
    var confidence: Float
    var boundingBox: OcrBoundingBox
    var pageIndex: Int
    var blockIndex: Int
    var lineIndex: Int
    var isBoundingBoxApproximate: Bool
}

struct OcrGraphPage: Codable {
    var pageIndex: Int
    var pageWidth: Float
    var pageHeight: Float
}

struct OcrDocumentGraph: Codable {
    var allTokens: [OcrToken]
    var pages: [OcrGraphPage]
    var detectedLanguage: String?
}

struct BuildStructuredDocumentInput: Codable {
    var documentId: String
    var ocrText: String
    var docType: String
    var classConfidence: Float
    var classMethod: String
    var useMlExtraction: Bool
    var ocrDurationMs: Int64
    var classificationDurationMs: Int64
    var graph: OcrDocumentGraph?
}
