import Foundation
import SwiftUI

@MainActor
class MobileEngineSessionStore: ObservableObject {
    // MARK: - Published State

    @Published var isInitialized = false
    @Published var isAuthenticated = false
    @Published var currentUser: UserInfo?
    @Published var patientProfile: PatientInfo?
    @Published var isLoading = false
    @Published var isHydratingAfterAuth = false
    @Published var errorMessage: String?
    @Published var hasCloudProfile = false
    @Published var isCloudAuthenticated = false

    // MARK: - Feature State

    @Published var medications: [MedicationInfo] = []
    @Published var latestEnvironmentReading: EnvironmentReadingInfo?
    @Published var chatMessages: [ChatMessageInfo] = []
    @Published var coachingAdvice: CoachingAdviceInfo?
    @Published var sleepSessions: [SleepSessionInfo] = []
    @Published var medicalHistory: [MedicalHistoryEntryInfo] = []
    @Published var ocrDocuments: [OcrDocumentInfo] = []
    @Published var assignedDoctors: [AssignedDoctorInfo] = []
    @Published var environmentAnalytics: EnvironmentAnalyticsInfo?
    @Published var environmentAdvice: CoachingAdviceInfo?

    // Vault State
    @Published var isVaultInitialized = false
    @Published var isVaultUnlocked = false

    // MARK: - Native Services

    @Published var healthKitService = HealthKitService()
    @Published var backgroundSyncService = BackgroundSyncService.shared
    @Published var biometricAuthService = BiometricAuthService()

    // MARK: - Private Properties

    private var engine: MobileEngineClient?
    private let databasePath: String
    private let apiBaseUrl: String
    private let geminiApiKey: String?
    private let openRouterApiKey: String?
    private let openRouterModel: String?
    private let openWeatherApiKey: String?
    private let googleOAuthClientId: String?

    init() {
        let documentsPath = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first!
        self.databasePath = documentsPath.appendingPathComponent("digitaltwin.db").path
        self.apiBaseUrl = Bundle.main.infoDictionary?["API_BASE_URL"] as? String ?? ""

        // API keys — injected from Secrets.xcconfig → Info.plist at build time
        self.geminiApiKey = Self.sanitizedConfigValue(Bundle.main.infoDictionary?["GEMINI_API_KEY"] as? String)
        self.openRouterApiKey = Self.sanitizedConfigValue(Bundle.main.infoDictionary?["OPENROUTER_API_KEY"] as? String)
        self.openRouterModel = Self.sanitizedConfigValue(Bundle.main.infoDictionary?["OPENROUTER_MODEL"] as? String)
        self.openWeatherApiKey = Self.sanitizedConfigValue(Bundle.main.infoDictionary?["OPENWEATHER_API_KEY"] as? String)
        self.googleOAuthClientId = Self.sanitizedConfigValue(Bundle.main.infoDictionary?["GOOGLE_OAUTH_CLIENT_ID"] as? String)

        if apiBaseUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || apiBaseUrl.contains("$(") {
            errorMessage = "Missing API_BASE_URL. Fill it in Secrets.xcconfig for cloud sync."
        }
        if geminiApiKey == nil && openRouterApiKey == nil {
            // Non-fatal: only affects Assistant feature.
            if errorMessage == nil {
                errorMessage = "Missing AI provider key. Fill GEMINI_API_KEY or OPENROUTER_API_KEY in Secrets.xcconfig to enable the AI assistant."
            }
        }
    }

    private static func sanitizedConfigValue(_ value: String?) -> String? {
        guard let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines),
              !trimmed.isEmpty,
              !trimmed.contains("$(") else {
            return nil
        }

        return trimmed
    }

    var databasePathString: String { databasePath }

    deinit {
        // We can't `await` in deinit; fire-and-forget cleanup.
        if let engine = engine {
            Task { await engine.dispose() }
        }
    }

    // MARK: - Initialization

    func initialize() async {
        guard !isInitialized else { return }

        do {
            engine = try DotNetMobileEngineClient(
                databasePath: databasePath,
                apiBaseUrl: apiBaseUrl,
                geminiApiKey: geminiApiKey,
                openWeatherApiKey: openWeatherApiKey,
                googleOAuthClientId: googleOAuthClientId,
                openRouterApiKey: openRouterApiKey,
                openRouterModel: openRouterModel
            )

            let initResult = try await engine?.initializeDatabase()
            if let result = initResult, result.success {
                isInitialized = true
                await initializeNativeServices()
            } else {
                throw EngineError.initializationFailed(initResult?.error ?? "Unknown error")
            }
        } catch {
            errorMessage = "Failed to initialize app: \(error.localizedDescription)"
        }
    }

    private func initializeNativeServices() async {
        healthKitService.refreshAuthorizationStatus()
        biometricAuthService.checkBiometricAvailability()
        backgroundSyncService.loadBackgroundSyncPreference()
    }

    // MARK: - Native Services Integration

    func requestHealthKitAuthorization() async -> Bool {
        do {
            try await healthKitService.requestAuthorization()
            return healthKitService.isAuthorized
        } catch {
            errorMessage = "HealthKit authorization failed: \(error.localizedDescription)"
            return false
        }
    }

    func enableBiometricAuth() async -> Bool {
        let success = await biometricAuthService.enableBiometricAuth()
        if !success { errorMessage = "Failed to enable biometric authentication" }
        return success
    }

    func authenticateWithBiometrics(reason: String = "Authenticate to access your health data") async -> Bool {
        let result = await biometricAuthService.authenticate(reason: reason)
        switch result {
        case .success:
            return true
        case .failure(let error):
            errorMessage = error.localizedDescription
            return false
        }
    }

    func enableBackgroundSync(_ enabled: Bool) {
        backgroundSyncService.setBackgroundSyncEnabled(enabled)
    }

    func performManualSync() async -> Bool {
        await backgroundSyncService.performManualSync()
    }

    // MARK: - Authentication

    func authenticate(googleIdToken: String) async -> Bool {
        guard let engine else { return false }

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            let result = try await engine.authenticate(googleIdToken: googleIdToken)
            if result.success {
                isAuthenticated = true
                currentUser = result.user
                hasCloudProfile = result.hasCloudProfile ?? false
                isCloudAuthenticated = !(result.accessToken?.isEmpty ?? true)

                // If the cloud has a profile, the .NET engine should have seeded local SQLite during auth.
                // Hydrate the in-memory state so UI can route correctly.
                isHydratingAfterAuth = true
                await loadPatientProfile()
                isHydratingAfterAuth = false

                let _ = await performSync()
                return true
            } else {
                errorMessage = result.errorMessage ?? "Authentication failed"
                isCloudAuthenticated = false
                return false
            }
        } catch {
            errorMessage = "Authentication failed: \(error.localizedDescription)"
            isCloudAuthenticated = false
            return false
        }
    }

    func getCurrentUser() async {
        guard let engine else { return }
        do {
            currentUser = try await engine.getCurrentUser()
            isAuthenticated = currentUser != nil
        } catch {
            // non-fatal
        }
    }

    // MARK: - Patient Profile

    func loadPatientProfile() async {
        guard let engine else { return }
        do { patientProfile = try await engine.getPatientProfile() } catch {}
    }

    func updatePatientProfile(_ update: PatientUpdateInfo) async -> Bool {
        guard let engine else { return false }

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            let result = try await engine.updatePatientProfile(update)
            if result.success {
                await loadPatientProfile()
                return true
            } else {
                errorMessage = result.error ?? "Update failed"
                return false
            }
        } catch {
            errorMessage = "Update failed: \(error.localizedDescription)"
            return false
        }
    }

    // MARK: - Sign Out

    func signOut() async {
        isAuthenticated = false
        currentUser = nil
        patientProfile = nil
        hasCloudProfile = false
        isCloudAuthenticated = false
        medications = []
        chatMessages = []
        ocrDocuments = []
        medicalHistory = []
        isVaultInitialized = false
        isVaultUnlocked = false
    }

    // MARK: - Latest Vitals (convenience)

    var latestVitals: LatestVitals? { nil }

    // MARK: - Vital Signs

    func recordVitalSign(_ vitalSign: VitalSignInput) async -> Bool {
        guard let engine else { return false }
        do { return try await engine.recordVitalSign(vitalSign).success } catch { return false }
    }

    func getVitalSigns(from: Date? = nil, to: Date? = nil) async -> [VitalSignInfo] {
        guard let engine else { return [] }
        do { return try await engine.getVitalSigns(from: from, to: to) } catch { return [] }
    }

    func getVitalSignsByType(_ type: VitalSignType, from: Date? = nil, to: Date? = nil) async -> [VitalSignInfo] {
        guard let engine else { return [] }
        do { return try await engine.getVitalSignsByType(type, from: from, to: to) } catch { return [] }
    }

    // MARK: - Medications

    func loadMedications() async {
        guard let engine else { return }
        do { medications = try await engine.getMedications() } catch {}
    }

    func addMedication(_ input: AddMedicationInput) async -> Bool {
        guard let engine else { return false }
        do {
            let result = try await engine.addMedication(input)
            if result.success { await loadMedications() }
            return result.success
        } catch {
            errorMessage = "Failed to add medication: \(error.localizedDescription)"
            return false
        }
    }

    func discontinueMedication(id: UUID, reason: String?) async -> Bool {
        guard let engine else { return false }
        do {
            let input = DiscontinueMedicationInput(medicationId: id, reason: reason)
            let result = try await engine.discontinueMedication(input)
            if result.success { await loadMedications() }
            return result.success
        } catch {
            return false
        }
    }

    func searchDrugs(query: String) async -> [DrugSearchResult] {
        guard let engine else { return [] }
        do { return try await engine.searchDrugs(query: query) } catch { return [] }
    }

    func checkInteractions(rxCuis: [String]) async -> [MedicationInteractionInfo] {
        guard let engine else { return [] }
        do { return try await engine.checkInteractions(rxCuis: rxCuis) } catch { return [] }
    }

    // MARK: - Environment

    func fetchEnvironmentReading(latitude: Double, longitude: Double) async {
        guard let engine else { return }
        do { latestEnvironmentReading = try await engine.getEnvironmentReading(latitude: latitude, longitude: longitude) } catch {}
    }

    func loadLatestEnvironmentReading() async {
        guard let engine else { return }
        do { latestEnvironmentReading = try await engine.getLatestEnvironmentReading() } catch {}
    }

    // MARK: - ECG

    func evaluateEcgFrame(samples: [Double], spO2: Double, heartRate: Double) async -> EcgEvaluationResult? {
        guard let engine else { return nil }
        do {
            let frame = EcgFrameInput(samples: samples, spO2: spO2, heartRate: heartRate, timestamp: Date())
            return try await engine.evaluateEcgFrame(frame)
        } catch {
            return nil
        }
    }

    // MARK: - AI Chat

    func sendChatMessage(_ message: String) async -> Bool {
        guard let engine else { return false }
        do {
            _ = try await engine.sendChatMessage(message)
            await loadChatHistory()
            return true
        } catch {
            errorMessage = "Failed to send message: \(error.localizedDescription)"
            return false
        }
    }

    func loadChatHistory() async {
        guard let engine else { return }
        do { chatMessages = try await engine.getChatHistory() } catch {}
    }

    func clearChatHistory() async -> Bool {
        guard let engine else { return false }
        do {
            let result = try await engine.clearChatHistory()
            if result.success { chatMessages = [] }
            return result.success
        } catch {
            return false
        }
    }

    // MARK: - Coaching

    func fetchCoachingAdvice(forceRefresh: Bool = false) async {
        if !forceRefresh, isAdviceFresh(coachingAdvice, maxAgeSeconds: 4 * 60 * 60) {
            return
        }

        guard let engine else { return }
        do { coachingAdvice = try await engine.getCoachingAdvice() } catch {}
    }

    // MARK: - Sleep

    func recordSleepSession(startTime: Date, endTime: Date, qualityScore: Double?) async -> Bool {
        guard let engine else { return false }
        let durationMinutes = Int(endTime.timeIntervalSince(startTime) / 60)
        let input = SleepSessionInput(startTime: startTime, endTime: endTime, durationMinutes: durationMinutes, qualityScore: qualityScore)
        do {
            let result = try await engine.recordSleepSession(input)
            if result.success { await loadSleepSessions() }
            return result.success
        } catch {
            return false
        }
    }

    func loadSleepSessions(from: Date? = nil, to: Date? = nil) async {
        guard let engine else { return }
        do { sleepSessions = try await engine.getSleepSessions(from: from, to: to) } catch {}
    }

    // MARK: - Medical History & OCR

    func loadMedicalHistory() async {
        guard let engine else { return }
        do { medicalHistory = try await engine.getMedicalHistory() } catch {}
    }

    func loadOcrDocuments() async {
        guard let engine else { return }
        do { ocrDocuments = try await engine.getOcrDocuments() } catch {}
    }

    // MARK: - OCR Text Processing

    func classifyDocument(_ ocrText: String) async -> String {
        guard let engine else { return "Unknown" }
        return await engine.classifyDocument(ocrText)
    }

    func processFullOcr(_ ocrText: String) async -> OcrProcessingResult? {
        guard let engine else { return nil }
        do { return try await engine.processFullOcr(ocrText) } catch { return nil }
    }

    func processFullOcrRawJson(_ ocrText: String) async -> String? {
        guard let engine else { return nil }
        do { return try await engine.processFullOcrRawJson(ocrText) } catch { return nil }
    }

    func saveOcrDocument(_ input: SaveOcrDocumentInput) async -> OcrDocumentInfo? {
        guard let engine else { return nil }
        do { return try await engine.saveOcrDocument(input) } catch { return nil }
    }

    func sanitizeText(_ text: String) async -> String {
        guard let engine else { return text }
        return await engine.sanitizeText(text)
    }

    // MARK: - Advanced OCR — Vault

    func vaultInitialize(_ input: VaultInitInput) async -> VaultResultInfo? {
        guard let engine else { return nil }
        do {
            let result = try await engine.vaultInitialize(input)
            if result.success { isVaultInitialized = true }
            print("[OCR Vault][SessionStore] vaultInitialize success=\(result.success) error=\(result.error ?? "nil")")
            return result
        } catch {
            print("[OCR Vault][SessionStore] vaultInitialize threw: \(error.localizedDescription)")
            return nil
        }
    }

    func vaultUnlock(masterKeyBase64: String) async -> VaultResultInfo? {
        guard let engine else { return nil }
        do {
            let result = try await engine.vaultUnlock(masterKeyBase64: masterKeyBase64)
            if result.success { isVaultUnlocked = true }
            print("[OCR Vault][SessionStore] vaultUnlock success=\(result.success) error=\(result.error ?? "nil")")
            return result
        } catch {
            print("[OCR Vault][SessionStore] vaultUnlock threw: \(error.localizedDescription)")
            return nil
        }
    }

    func vaultLock() async -> VaultResultInfo? {
        guard let engine else { return nil }
        do {
            let result = try await engine.vaultLock()
            if result.success { isVaultUnlocked = false }
            print("[OCR Vault][SessionStore] vaultLock success=\(result.success) error=\(result.error ?? "nil")")
            return result
        } catch {
            print("[OCR Vault][SessionStore] vaultLock threw: \(error.localizedDescription)")
            return nil
        }
    }

    func vaultStoreDocument(_ input: VaultStoreDocumentInput) async -> VaultResultInfo? {
        guard let engine else { return nil }
        do {
            let result = try await engine.vaultStoreDocument(input)
            print("[OCR Vault][SessionStore] vaultStoreDocument success=\(result.success) error=\(result.error ?? "nil") docId=\(result.documentId ?? "nil")")
            return result
        } catch {
            print("[OCR Vault][SessionStore] vaultStoreDocument threw: \(error.localizedDescription)")
            return nil
        }
    }

    func vaultRetrieveDocument(documentId: String) async -> String? {
        guard let engine else { return nil }
        do {
            let payload = try await engine.vaultRetrieveDocument(documentId: documentId)
            print("[OCR Vault][SessionStore] vaultRetrieveDocument success docId=\(documentId) base64Length=\(payload.count)")
            return payload
        } catch {
            print("[OCR Vault][SessionStore] vaultRetrieveDocument threw for docId=\(documentId): \(error.localizedDescription)")
            return nil
        }
    }

    func vaultDeleteDocument(documentId: String) async -> VaultResultInfo? {
        guard let engine else { return nil }
        do {
            let result = try await engine.vaultDeleteDocument(documentId: documentId)
            print("[OCR Vault][SessionStore] vaultDeleteDocument success=\(result.success) error=\(result.error ?? "nil") docId=\(documentId)")
            return result
        } catch {
            print("[OCR Vault][SessionStore] vaultDeleteDocument threw for docId=\(documentId): \(error.localizedDescription)")
            return nil
        }
    }

    func vaultWipe() async -> VaultResultInfo? {
        guard let engine else { return nil }
        do {
            let result = try await engine.vaultWipe()
            if result.success {
                isVaultInitialized = false
                isVaultUnlocked = false
            }
            print("[OCR Vault][SessionStore] vaultWipe success=\(result.success) error=\(result.error ?? "nil")")
            return result
        } catch {
            print("[OCR Vault][SessionStore] vaultWipe threw: \(error.localizedDescription)")
            return nil
        }
    }

    // MARK: - Advanced OCR — Classification & Structured

    func classifyWithOrchestrator(ocrText: String, mlType: String? = nil, mlConfidence: Float = 0) async -> ClassificationResultInfo? {
        guard let engine else { return nil }
        do { return try await engine.classifyWithOrchestrator(ocrText: ocrText, mlType: mlType, mlConfidence: mlConfidence) } catch { return nil }
    }

    func buildStructuredDocument(ocrText: String, docType: String, classConfidence: Float, classMethod: String) async -> StructuredMedicalDocumentInfo? {
        guard let engine else { return nil }
        do { return try await engine.buildStructuredDocument(ocrText: ocrText, docType: docType, classConfidence: classConfidence, classMethod: classMethod) } catch { return nil }
    }

    func buildStructuredDocumentFromJson(_ input: BuildStructuredDocumentInput) async -> StructuredMedicalDocumentInfo? {
        guard let engine else { return nil }
        do { return try await engine.buildStructuredDocumentFromJson(input) } catch { return nil }
    }

    func getMlAuditSummary() async -> MlAuditSummaryInfo? {
        guard let engine else { return nil }
        do { return try await engine.getMlAuditSummary() } catch { return nil }
    }

    func validateDocument(headerBase64: String, fileExtension: String, fileSizeBytes: Int64) async -> VaultResultInfo? {
        guard let engine else { return nil }
        do { return try await engine.validateDocument(headerBase64: headerBase64, fileExtension: fileExtension, fileSizeBytes: fileSizeBytes) } catch { return nil }
    }

    // MARK: - Doctor Assignment

    func loadAssignedDoctors() async {
        guard let engine else { return }
        do { assignedDoctors = try await engine.getAssignedDoctors() } catch {}
    }

    // MARK: - Local Data Reset

    func resetLocalData() async -> Bool {
        guard let engine else { return false }
        do {
            let result = try await engine.resetLocalData()
            if result.success {
                // Clear all in-memory state
                medications = []
                chatMessages = []
                ocrDocuments = []
                medicalHistory = []
                sleepSessions = []
                assignedDoctors = []
                environmentAnalytics = nil
                environmentAdvice = nil
                coachingAdvice = nil
                latestEnvironmentReading = nil
            }
            return result.success
        } catch {
            errorMessage = "Failed to reset data: \(error.localizedDescription)"
            return false
        }
    }

    // MARK: - Environment Analytics

    func loadEnvironmentAnalytics() async {
        guard let engine else { return }
        do { environmentAnalytics = try await engine.getEnvironmentAnalytics() } catch {}
    }

    func loadEnvironmentAdvice(forceRefresh: Bool = false) async {
        if !forceRefresh, isAdviceFresh(environmentAdvice, maxAgeSeconds: 2 * 60 * 60) {
            return
        }

        guard let engine else { return }
        do { environmentAdvice = try await engine.getEnvironmentAdvice() } catch {}
    }

    private func isAdviceFresh(_ advice: CoachingAdviceInfo?, maxAgeSeconds: TimeInterval) -> Bool {
        guard let advice else { return false }
        return Date().timeIntervalSince(advice.timestamp) < maxAgeSeconds
    }

    // MARK: - Synchronization

    func performSync() async -> Bool {
        guard let engine else { return false }

        if !isCloudAuthenticated {
            await getCurrentUser()
            await loadPatientProfile()
            return true
        }

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            if healthKitService.isAuthorized {
                await syncHealthKitData()
            }

            let result = try await engine.performSync()

            if result.success {
                if healthKitService.isAuthorized {
                    await writeVitalsToHealthKit()
                }

                await getCurrentUser()
                await loadPatientProfile()
                return true
            } else {
                errorMessage = result.error ?? "Sync failed"
                return false
            }
        } catch {
            errorMessage = "Sync failed: \(error.localizedDescription)"
            return false
        }
    }

    private func syncHealthKitData() async {
        do {
            let endDate = Date()
            let startDate = Calendar.current.date(byAdding: .day, value: -7, to: endDate) ?? endDate
            let healthKitVitals = try await healthKitService.readVitalSigns(from: startDate, to: endDate)

            for vital in healthKitVitals {
                let vitalInput = VitalSignInput(
                    type: vital.type,
                    value: vital.value,
                    unit: vital.unit,
                    source: vital.source,
                    timestamp: vital.timestamp
                )
                let _ = await recordVitalSign(vitalInput)
            }
        } catch {}
    }

    private func writeVitalsToHealthKit() async {
        do {
            let endDate = Date()
            let startDate = Calendar.current.date(byAdding: .hour, value: -1, to: endDate) ?? endDate

            let recentVitals = await getVitalSigns(from: startDate, to: endDate)
            let unsyncedVitals = recentVitals.filter { !$0.isSynced && $0.source != "HealthKit" }

            let vitalInputs = unsyncedVitals.map { vital in
                VitalSignInput(
                    type: vital.type,
                    value: vital.value,
                    unit: vital.unit,
                    source: "DigitalTwin",
                    timestamp: vital.timestamp
                )
            }

            if !vitalInputs.isEmpty {
                try await healthKitService.writeVitalSigns(vitalInputs)
            }
        } catch {}
    }

    func pushLocalChanges() async -> Bool {
        guard let engine else { return false }
        guard isCloudAuthenticated else { return true }
        do { return try await engine.pushLocalChanges().success } catch { return false }
    }
}

