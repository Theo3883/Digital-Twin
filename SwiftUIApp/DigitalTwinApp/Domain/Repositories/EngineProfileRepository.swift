import Foundation

@MainActor
final class EngineProfileRepository: ProfileRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadCurrentUser() async -> UserInfo? {
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

    func updatePatientProfile(_ input: PatientUpdateInfo) async {
        _ = await engine.updatePatientProfile(input)
        await engine.loadPatientProfile()
    }

    func signOut() async {
        await engine.signOut()
    }
}

