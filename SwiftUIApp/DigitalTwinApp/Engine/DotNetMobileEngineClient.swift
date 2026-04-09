import Foundation

actor DotNetMobileEngineClient: MobileEngineClient {
    private let handle: MobileEngineHandle

    init(
        databasePath: String,
        apiBaseUrl: String,
        geminiApiKey: String? = nil,
        openWeatherApiKey: String? = nil,
        googleOAuthClientId: String? = nil
    ) throws {
        self.handle = try MobileEngineHandle(
            databasePath: databasePath,
            apiBaseUrl: apiBaseUrl,
            geminiApiKey: geminiApiKey,
            openWeatherApiKey: openWeatherApiKey,
            googleOAuthClientId: googleOAuthClientId
        )
    }

    func dispose() async {
        await handle.dispose()
    }

    func initializeDatabase() async throws -> OperationResult { try await handle.initializeDatabase() }
    func authenticate(googleIdToken: String) async throws -> AuthenticationResult { try await handle.authenticate(googleIdToken: googleIdToken) }
    func getCurrentUser() async throws -> UserInfo? { try await handle.getCurrentUser() }
    func getPatientProfile() async throws -> PatientInfo? { try await handle.getPatientProfile() }
    func updatePatientProfile(_ update: PatientUpdateInfo) async throws -> OperationResult { try await handle.updatePatientProfile(update) }

    func recordVitalSign(_ vitalSign: VitalSignInput) async throws -> OperationResult { try await handle.recordVitalSign(vitalSign) }
    func getVitalSigns(from: Date?, to: Date?) async throws -> [VitalSignInfo] { try await handle.getVitalSigns(from: from, to: to) }
    func getVitalSignsByType(_ type: VitalSignType, from: Date?, to: Date?) async throws -> [VitalSignInfo] { try await handle.getVitalSignsByType(type, from: from, to: to) }

    func performSync() async throws -> OperationResult { try await handle.performSync() }
    func pushLocalChanges() async throws -> OperationResult { try await handle.pushLocalChanges() }

    func getMedications() async throws -> [MedicationInfo] { try await handle.getMedications() }
    func addMedication(_ input: AddMedicationInput) async throws -> OperationResult { try await handle.addMedication(input) }
    func discontinueMedication(_ input: DiscontinueMedicationInput) async throws -> OperationResult { try await handle.discontinueMedication(input) }
    func searchDrugs(query: String) async throws -> [DrugSearchResult] { try await handle.searchDrugs(query: query) }
    func checkInteractions(rxCuis: [String]) async throws -> [MedicationInteractionInfo] { try await handle.checkInteractions(rxCuis: rxCuis) }

    func getEnvironmentReading(latitude: Double, longitude: Double) async throws -> EnvironmentReadingInfo { try await handle.getEnvironmentReading(latitude: latitude, longitude: longitude) }
    func getLatestEnvironmentReading() async throws -> EnvironmentReadingInfo? { try await handle.getLatestEnvironmentReading() }

    func evaluateEcgFrame(_ frame: EcgFrameInput) async throws -> EcgEvaluationResult { try await handle.evaluateEcgFrame(frame) }

    func sendChatMessage(_ message: String) async throws -> ChatMessageInfo { try await handle.sendChatMessage(message) }
    func getChatHistory() async throws -> [ChatMessageInfo] { try await handle.getChatHistory() }
    func clearChatHistory() async throws -> OperationResult { try await handle.clearChatHistory() }

    func getCoachingAdvice() async throws -> CoachingAdviceInfo { try await handle.getCoachingAdvice() }

    func recordSleepSession(_ input: SleepSessionInput) async throws -> OperationResult { try await handle.recordSleepSession(input) }
    func getSleepSessions(from: Date?, to: Date?) async throws -> [SleepSessionInfo] { try await handle.getSleepSessions(from: from, to: to) }

    func getMedicalHistory() async throws -> [MedicalHistoryEntryInfo] { try await handle.getMedicalHistory() }
    func getOcrDocuments() async throws -> [OcrDocumentInfo] { try await handle.getOcrDocuments() }

    func classifyDocument(_ ocrText: String) async -> String { await handle.classifyDocument(ocrText) }
    func processFullOcr(_ ocrText: String) async throws -> OcrProcessingResult { try await handle.processFullOcr(ocrText) }
    func saveOcrDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async throws -> OcrDocumentInfo {
        try await handle.saveOcrDocument(
            opaqueInternalName: opaqueInternalName,
            mimeType: mimeType,
            pageCount: pageCount,
            pageTexts: pageTexts
        )
    }
    func sanitizeText(_ text: String) async -> String { await handle.sanitizeText(text) }
}

