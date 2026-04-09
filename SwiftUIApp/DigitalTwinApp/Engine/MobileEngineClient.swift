import Foundation

protocol MobileEngineClient: Sendable {
    func dispose() async

    func initializeDatabase() async throws -> OperationResult
    func authenticate(googleIdToken: String) async throws -> AuthenticationResult
    func getCurrentUser() async throws -> UserInfo?
    func getPatientProfile() async throws -> PatientInfo?
    func updatePatientProfile(_ update: PatientUpdateInfo) async throws -> OperationResult

    func recordVitalSign(_ vitalSign: VitalSignInput) async throws -> OperationResult
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

    // MARK: - Medical History & OCR
    func getMedicalHistory() async throws -> [MedicalHistoryEntryInfo]
    func getOcrDocuments() async throws -> [OcrDocumentInfo]

    // MARK: - OCR Text Processing
    func classifyDocument(_ ocrText: String) async -> String
    func processFullOcr(_ ocrText: String) async throws -> OcrProcessingResult
    func saveOcrDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async throws -> OcrDocumentInfo
    func sanitizeText(_ text: String) async -> String
}

