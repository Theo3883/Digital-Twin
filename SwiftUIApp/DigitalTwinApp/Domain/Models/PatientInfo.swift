import Foundation

struct PatientInfo: Codable, Identifiable {
    let id: UUID
    let userId: UUID
    let bloodType: String?
    let allergies: String?
    let medicalHistoryNotes: String?
    let weight: Double?
    let height: Double?
    let bloodPressureSystolic: Int?
    let bloodPressureDiastolic: Int?
    let cholesterol: Double?
    let cnp: String?
    let isSynced: Bool
}

