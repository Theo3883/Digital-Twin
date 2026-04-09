import Foundation

protocol ProfileRepository: Sendable {
    func loadCurrentUser() async -> UserInfo?
    func loadPatientProfile() async -> PatientInfo?
    func loadMedicalHistory() async -> [MedicalHistoryEntryInfo]
    func loadOcrDocuments() async -> [OcrDocumentInfo]
    func latestHeartRate() async -> Int?
    func updatePatientProfile(_ input: PatientUpdateInfo) async
    func signOut() async
}

