import Foundation

@MainActor
final class EngineProfileRepository: ProfileRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadCurrentUser() async -> UserInfo? {
        // Check if already cached from auth/restoreCachedSession
        if engine.currentUser != nil {
            return engine.currentUser
        }
        // Only fetch if cache is cold
        await engine.getCurrentUser()
        return engine.currentUser
    }

    func loadPatientProfile() async -> PatientInfo? {
        await engine.loadPatientProfile()
        return engine.patientProfile
    }

    func loadMedicalHistory() async -> [MedicalHistoryEntryInfo] {
        await engine.loadMedicalHistory()
        return engine.medicalHistory
    }

    func loadOcrDocuments() async -> [OcrDocumentInfo] {
        await engine.loadOcrDocuments()
        return engine.ocrDocuments
    }

    func latestHeartRate() async -> Int? {
        engine.latestVitals?.heartRate
    }

    func updateUserProfile(_ input: UserUpdateInfo) async -> Bool {
        await engine.updateUserProfile(input)
    }

    func updatePatientProfile(_ input: PatientUpdateInfo) async -> Bool {
        await engine.updatePatientProfile(input)
    }

    func signOut() async {
        await engine.signOut()
    }
}

