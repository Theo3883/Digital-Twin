import Foundation

/// Swift handle to the .NET Mobile Engine via C bridge.
/// Implemented as an actor to satisfy Swift 6 concurrency checks.
actor MobileEngineHandle {
    private let bridge = DotNetBridge()

    init(databasePath: String, apiBaseUrl: String, geminiApiKey: String? = nil, openWeatherApiKey: String? = nil, googleOAuthClientId: String? = nil) throws {
        let result = try bridge.initialize(
            databasePath: databasePath,
            apiBaseUrl: apiBaseUrl,
            geminiApiKey: geminiApiKey,
            openWeatherApiKey: openWeatherApiKey,
            googleOAuthClientId: googleOAuthClientId
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

    func getPatientProfile() async throws -> PatientInfo? {
        try bridge.getPatientProfile()
    }

    func updatePatientProfile(_ update: PatientUpdateInfo) async throws -> OperationResult {
        try bridge.updatePatientProfile(update)
    }

    func recordVitalSign(_ vitalSign: VitalSignInput) async throws -> OperationResult {
        try bridge.recordVitalSign(vitalSign)
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
        try bridge.evaluateEcgFrame(frame)
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

    func saveOcrDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async throws -> OcrDocumentInfo {
        try bridge.saveOcrDocument(
            opaqueInternalName: opaqueInternalName,
            mimeType: mimeType,
            pageCount: pageCount,
            pageTexts: pageTexts
        )
    }

    func sanitizeText(_ text: String) async -> String {
        bridge.sanitizeText(text)
    }
}

