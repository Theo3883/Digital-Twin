import Foundation

/// Swift handle to the .NET Mobile Engine via C bridge.
/// Implemented as an actor to satisfy Swift 6 concurrency checks.
actor MobileEngineHandle {
    private let bridge = DotNetBridge()

    init(databasePath: String, apiBaseUrl: String, geminiApiKey: String? = nil, openWeatherApiKey: String? = nil, googleOAuthClientId: String? = nil, openRouterApiKey: String? = nil, openRouterModel: String? = nil) throws {
        let result = try bridge.initialize(
            databasePath: databasePath,
            apiBaseUrl: apiBaseUrl,
            geminiApiKey: geminiApiKey,
            openWeatherApiKey: openWeatherApiKey,
            googleOAuthClientId: googleOAuthClientId,
            openRouterApiKey: openRouterApiKey,
            openRouterModel: openRouterModel
        )
        if !result.success {
            throw EngineError.initializationFailed(result.error ?? "Unknown error")
        }
    }

    func dispose() {
        bridge.dispose()
    }

    // MARK: - Existing Bridge Methods

    func initializeDatabase() async throws -> OperationResult {
        try bridge.initializeDatabase()
    }

    func authenticate(googleIdToken: String) async throws -> AuthenticationResult {
        try bridge.authenticate(googleIdToken: googleIdToken)
    }

    func getCurrentUser() async throws -> UserInfo? {
        try bridge.getCurrentUser()
    }

    func updateCurrentUser(_ update: UserUpdateInfo) async throws -> OperationResult {
        try bridge.updateCurrentUser(update)
    }

    func getPatientProfile() async throws -> PatientInfo? {
        try bridge.getPatientProfile()
    }

    func updatePatientProfile(_ update: PatientUpdateInfo) async throws -> OperationResult {
        try bridge.updatePatientProfile(update)
    }

    func recordVitalSign(_ vitalSign: VitalSignInput) async throws -> OperationResult {
        try bridge.recordVitalSign(vitalSign)
    }

    func recordVitalSigns(_ vitalSigns: [VitalSignInput]) async throws -> RecordVitalSignsResult {
        try bridge.recordVitalSigns(vitalSigns)
    }

    func getVitalSigns(from: Date?, to: Date?) async throws -> [VitalSignInfo] {
        try bridge.getVitalSigns(from: from, to: to)
    }

    func getVitalSignsByType(_ type: VitalSignType, from: Date?, to: Date?) async throws -> [VitalSignInfo] {
        try bridge.getVitalSignsByType(type, from: from, to: to)
    }

    func performSync() async throws -> OperationResult {
        try bridge.performSync()
    }

    func pushLocalChanges() async throws -> OperationResult {
        try bridge.pushLocalChanges()
    }

    // MARK: - Medications

    func getMedications() async throws -> [MedicationInfo] {
        try bridge.getMedications()
    }

    func addMedication(_ input: AddMedicationInput) async throws -> OperationResult {
        try bridge.addMedication(input)
    }

    func discontinueMedication(_ input: DiscontinueMedicationInput) async throws -> OperationResult {
        try bridge.discontinueMedication(input)
    }

    func searchDrugs(query: String) async throws -> [DrugSearchResult] {
        try bridge.searchDrugs(query: query)
    }

    func checkInteractions(rxCuis: [String]) async throws -> [MedicationInteractionInfo] {
        try bridge.checkInteractions(rxCuis: rxCuis)
    }

    // MARK: - Environment

    func getEnvironmentReading(latitude: Double, longitude: Double) async throws -> EnvironmentReadingInfo {
        try bridge.getEnvironmentReading(latitude: latitude, longitude: longitude)
    }

    func getLatestEnvironmentReading() async throws -> EnvironmentReadingInfo? {
        try bridge.getLatestEnvironmentReading()
    }

    // MARK: - ECG

    func evaluateEcgFrame(_ frame: EcgFrameInput) async throws -> EcgEvaluationResult {
        // DotNetBridge.evaluateEcgFrame returns the raw JSON from .NET which has this shape:
        //   { "frame": { "triageResult": "Pass"|"Critical", "spO2": 98.2, "heartRate": 72, ... },
        //     "alert": { "ruleName": "...", "message": "...", "timestamp": "..." } | null }
        //
        // This does NOT match EcgEvaluationResult which expects { "triageResult": Int, "alerts": [...] }.
        // We decode into EcgEngineResponse and map to EcgEvaluationResult explicitly.
        let engineResponse = try bridge.evaluateEcgFrame(frame)
        return EcgEvaluationResult(
            triageResult: engineResponse.frame?.triageResult == "Critical" ? 2 : 0,
            alerts: engineResponse.alert.map { [$0.message] } ?? [],
            heartRate: Double(engineResponse.frame?.heartRate ?? 0),
            spO2: engineResponse.frame?.spO2 ?? 0,
            signalQualityPassed: engineResponse.alert?.ruleName != "SignalQuality"
        )
    }

    // MARK: - AI Chat

    func sendChatMessage(_ message: String) async throws -> ChatMessageInfo {
        try bridge.sendChatMessage(message)
    }

    func getChatHistory() async throws -> [ChatMessageInfo] {
        try bridge.getChatHistory()
    }

    func clearChatHistory() async throws -> OperationResult {
        try bridge.clearChatHistory()
    }

    // MARK: - Coaching

    func getCoachingAdvice() async throws -> CoachingAdviceInfo {
        try bridge.getCoachingAdvice()
    }

    // MARK: - Sleep

    func recordSleepSession(_ input: SleepSessionInput) async throws -> OperationResult {
        try bridge.recordSleepSession(input)
    }

    func getSleepSessions(from: Date?, to: Date?) async throws -> [SleepSessionInfo] {
        try bridge.getSleepSessions(from: from, to: to)
    }

    // MARK: - Doctor Assignment

    func getAssignedDoctors() async throws -> [AssignedDoctorInfo] {
        try bridge.getAssignedDoctors()
    }

    // MARK: - Local Data Reset

    func resetLocalData() async throws -> OperationResult {
        try bridge.resetLocalData()
    }

    // MARK: - Environment Analytics

    func getEnvironmentAnalytics() async throws -> EnvironmentAnalyticsInfo {
        try bridge.getEnvironmentAnalytics()
    }

    func getEnvironmentAdvice() async throws -> CoachingAdviceInfo {
        try bridge.getEnvironmentAdvice()
    }

    // MARK: - Medical History & OCR

    func getMedicalHistory() async throws -> [MedicalHistoryEntryInfo] {
        try bridge.getMedicalHistory()
    }

    func getOcrDocuments() async throws -> [OcrDocumentInfo] {
        try bridge.getOcrDocuments()
    }

    // MARK: - OCR Text Processing

    func classifyDocument(_ ocrText: String) async -> String {
        bridge.classifyDocument(ocrText)
    }

    func processFullOcr(_ ocrText: String) async throws -> OcrProcessingResult {
        try bridge.processFullOcr(ocrText)
    }

    func processFullOcrRawJson(_ ocrText: String) async throws -> String {
        try bridge.processFullOcrRawJson(ocrText)
    }

    func saveOcrDocument(_ input: SaveOcrDocumentInput) async throws -> OcrDocumentInfo {
        try bridge.saveOcrDocument(input)
    }

    func sanitizeText(_ text: String) async -> String {
        bridge.sanitizeText(text)
    }

    // MARK: - Advanced OCR — Vault

    func vaultInitialize(_ input: VaultInitInput) async throws -> VaultResultInfo {
        try bridge.vaultInitialize(input)
    }

    func vaultUnlock(masterKeyBase64: String) async throws -> VaultResultInfo {
        try bridge.vaultUnlock(masterKeyBase64: masterKeyBase64)
    }

    func vaultLock() async throws -> VaultResultInfo {
        try bridge.vaultLock()
    }

    func vaultStoreDocument(_ input: VaultStoreDocumentInput) async throws -> VaultResultInfo {
        try bridge.vaultStoreDocument(input)
    }

    func vaultRetrieveDocument(documentId: String) async throws -> String {
        try bridge.vaultRetrieveDocument(documentId: documentId)
    }

    func vaultDeleteDocument(documentId: String) async throws -> VaultResultInfo {
        try bridge.vaultDeleteDocument(documentId: documentId)
    }

    func vaultWipe() async throws -> VaultResultInfo {
        try bridge.vaultWipe()
    }

    // MARK: - Advanced OCR — Classification & Structured

    func classifyWithOrchestrator(ocrText: String, mlType: String?, mlConfidence: Float) async throws -> ClassificationResultInfo {
        try bridge.classifyWithOrchestrator(ocrText: ocrText, mlType: mlType, mlConfidence: mlConfidence)
    }

    func buildStructuredDocument(ocrText: String, docType: String, classConfidence: Float, classMethod: String) async throws -> StructuredMedicalDocumentInfo {
        try bridge.buildStructuredDocument(ocrText: ocrText, docType: docType, classConfidence: classConfidence, classMethod: classMethod)
    }

    func buildStructuredDocumentFromJson(_ input: BuildStructuredDocumentInput) async throws -> StructuredMedicalDocumentInfo {
        try bridge.buildStructuredDocumentFromJson(input)
    }

    func getMlAuditSummary() async throws -> MlAuditSummaryInfo {
        try bridge.getMlAuditSummary()
    }

    func validateDocument(headerBase64: String, fileExtension: String, fileSizeBytes: Int64) async throws -> VaultResultInfo {
        try bridge.validateDocument(headerBase64: headerBase64, fileExtension: fileExtension, fileSizeBytes: fileSizeBytes)
    }
}

