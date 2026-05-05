import Foundation

struct HeuristicExtractionInfo: Codable {
    let patientName: String?
    let patientId: String?
    let reportDate: String?
    let doctorName: String?
    let diagnosis: String?
    let medications: [ExtractedMedicationFieldInfo]
}

