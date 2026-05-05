import Foundation

@MainActor
final class ProfileViewModel: ObservableObject {
    @Published private(set) var currentUser: UserInfo?
    @Published private(set) var patient: PatientInfo?
    @Published private(set) var medicalHistory: [MedicalHistoryEntryInfo] = []
    @Published private(set) var ocrDocuments: [OcrDocumentInfo] = []
    @Published private(set) var latestHeartRate: Int?
    @Published private(set) var assignedDoctors: [AssignedDoctorInfo] = []

    private let repository: ProfileRepository
    private let getDoctors: GetAssignedDoctorsUseCase?

    init(repository: ProfileRepository, getDoctors: GetAssignedDoctorsUseCase? = nil) {
        self.repository = repository
        self.getDoctors = getDoctors
    }

    func load() async {
        async let user = repository.loadCurrentUser()
        async let patient = repository.loadPatientProfile()
        async let history = repository.loadMedicalHistory()
        async let docs = repository.loadOcrDocuments()
        async let hr = repository.latestHeartRate()

        self.currentUser = await user
        self.patient = await patient
        self.medicalHistory = await history
        self.ocrDocuments = await docs
        self.latestHeartRate = await hr

        if let getDoctors {
            self.assignedDoctors = await getDoctors()
        }
    }

    func signOut() {
        Task { await repository.signOut() }
    }
}

