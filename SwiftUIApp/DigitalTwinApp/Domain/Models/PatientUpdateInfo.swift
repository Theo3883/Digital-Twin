import Foundation

struct PatientUpdateInfo: Codable {
    let bloodType: String?
    let allergies: String?
    let medicalHistoryNotes: String?
    let weight: Double?
    let height: Double?
    let bloodPressureSystolic: Int?
    let bloodPressureDiastolic: Int?
    let cholesterol: Double?
    let cnp: String?
}

