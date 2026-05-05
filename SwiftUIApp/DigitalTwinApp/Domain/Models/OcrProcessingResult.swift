import Foundation

struct OcrProcessingResult: Codable {
    let documentType: String
    let identity: DocumentIdentityInfo?
    let validation: IdentityValidationInfo?
    let sanitizedText: String
    let extraction: HeuristicExtractionInfo?
    let historyItems: [ExtractedHistoryItemInfo]
}

