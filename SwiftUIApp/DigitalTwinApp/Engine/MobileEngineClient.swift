import Foundation

protocol MobileEngineClient: Sendable {
    func dispose() async

    func initializeDatabase() async throws -> OperationResult
    func authenticate(googleIdToken: String) async throws -> AuthenticationResult
    func getCurrentUser() async throws -> UserInfo?
    func updateCurrentUser(_ update: UserUpdateInfo) async throws -> OperationResult
    func getPatientProfile() async throws -> PatientInfo?
    func updatePatientProfile(_ update: PatientUpdateInfo) async throws -> OperationResult

    func recordVitalSign(_ vitalSign: VitalSignInput) async throws -> OperationResult
    func recordVitalSigns(_ vitalSigns: [VitalSignInput]) async throws -> RecordVitalSignsResult
    func getVitalSigns(from: Date?, to: Date?) async throws -> [VitalSignInfo]
    func getVitalSignsByType(_ type: VitalSignType, from: Date?, to: Date?) async throws -> [VitalSignInfo]

    func performSync() async throws -> OperationResult
    func pushLocalChanges() async throws -> OperationResult

    // MARK: - Medications
    func getMedications() async throws -> [MedicationInfo]
    func addMedication(_ input: AddMedicationInput) async throws -> OperationResult
    func discontinueMedication(_ input: DiscontinueMedicationInput) async throws -> OperationResult
    func searchDrugs(query: String) async throws -> [DrugSearchResult]
    func checkInteractions(rxCuis: [String]) async throws -> [MedicationInteractionInfo]

    // MARK: - Environment
    func getEnvironmentReading(latitude: Double, longitude: Double) async throws -> EnvironmentReadingInfo
    func getLatestEnvironmentReading() async throws -> EnvironmentReadingInfo?

    // MARK: - ECG
    func evaluateEcgFrame(_ frame: EcgFrameInput) async throws -> EcgEvaluationResult

    // MARK: - AI Chat
    func sendChatMessage(_ message: String) async throws -> ChatMessageInfo
    func getChatHistory() async throws -> [ChatMessageInfo]
    func clearChatHistory() async throws -> OperationResult

    // MARK: - Coaching
    func getCoachingAdvice() async throws -> CoachingAdviceInfo

    // MARK: - Sleep
    func recordSleepSession(_ input: SleepSessionInput) async throws -> OperationResult
    func getSleepSessions(from: Date?, to: Date?) async throws -> [SleepSessionInfo]

    // MARK: - Doctor Assignment
    func getAssignedDoctors() async throws -> [AssignedDoctorInfo]

    // MARK: - Notifications
    func getNotifications(limit: Int, unreadOnly: Bool) async throws -> [NotificationInfo]

    // MARK: - Cloud session restore
    func setCloudAccessToken(_ token: String) async throws -> OperationResult
    func getCloudAuthStatus() async throws -> Bool

    // MARK: - Local Data Reset
    func resetLocalData() async throws -> OperationResult

    // MARK: - Environment Analytics
    func getEnvironmentAnalytics() async throws -> EnvironmentAnalyticsInfo
    func getEnvironmentAdvice() async throws -> CoachingAdviceInfo

    // MARK: - Medical History & OCR
    func getMedicalHistory() async throws -> [MedicalHistoryEntryInfo]
    func getOcrDocuments() async throws -> [OcrDocumentInfo]

    // MARK: - OCR Text Processing
    func classifyDocument(_ ocrText: String) async -> String
    func processFullOcr(_ ocrText: String) async throws -> OcrProcessingResult
    func processFullOcrRawJson(_ ocrText: String) async throws -> String
    func saveOcrDocument(_ input: SaveOcrDocumentInput) async throws -> OcrDocumentInfo
    func sanitizeText(_ text: String) async -> String

    // MARK: - Advanced OCR — Vault
    func vaultInitialize(_ input: VaultInitInput) async throws -> VaultResultInfo
    func vaultUnlock(masterKeyBase64: String) async throws -> VaultResultInfo
    func vaultLock() async throws -> VaultResultInfo
    func vaultStoreDocument(_ input: VaultStoreDocumentInput) async throws -> VaultResultInfo
    func vaultRetrieveDocument(documentId: String) async throws -> String
    func vaultDeleteDocument(documentId: String) async throws -> VaultResultInfo
    func vaultWipe() async throws -> VaultResultInfo

    // MARK: - Advanced OCR — Classification & Structured
    func classifyWithOrchestrator(ocrText: String, mlType: String?, mlConfidence: Float) async throws -> ClassificationResultInfo
    func buildStructuredDocument(ocrText: String, docType: String, classConfidence: Float, classMethod: String) async throws -> StructuredMedicalDocumentInfo
    func buildStructuredDocumentFromJson(_ input: BuildStructuredDocumentInput) async throws -> StructuredMedicalDocumentInfo
    func getMlAuditSummary() async throws -> MlAuditSummaryInfo
    func validateDocument(headerBase64: String, fileExtension: String, fileSizeBytes: Int64) async throws -> VaultResultInfo
}

