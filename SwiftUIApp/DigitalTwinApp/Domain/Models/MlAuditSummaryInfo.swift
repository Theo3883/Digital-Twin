import Foundation

struct MlAuditSummaryInfo: Codable {
    let totalDocuments: Int
    let averageOcrMs: Double
    let averageClassifyMs: Double
    let averageExtractMs: Double
    let averageConfidence: Double
    let bertUsagePercent: Double
}
