import Foundation

protocol ProfileRepository: Sendable {
    func loadCurrentUser() async -> UserInfo?
    func loadPatientProfile() async -> PatientInfo?
    func loadMedicalHistory() async -> [MedicalHistoryEntryInfo]
    func loadOcrDocuments() async -> [OcrDocumentInfo]
    func latestHeartRate() async -> Int?
    func updateUserProfile(_ input: UserUpdateInfo) async -> Bool
    func updatePatientProfile(_ input: PatientUpdateInfo) async -> Bool
    func signOut() async
}

