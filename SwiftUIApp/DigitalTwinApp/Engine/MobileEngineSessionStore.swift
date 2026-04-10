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

    // MARK: - Native Services

    @Published var healthKitService = HealthKitService()
    @Published var backgroundSyncService = BackgroundSyncService.shared
    @Published var biometricAuthService = BiometricAuthService()

    // MARK: - Private Properties

    private var engine: MobileEngineClient?
    private let databasePath: String
    private let apiBaseUrl: String
    private let geminiApiKey: String?
    private let openWeatherApiKey: String?
    private let googleOAuthClientId: String?

    init() {
        let documentsPath = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first!
        self.databasePath = documentsPath.appendingPathComponent("digitaltwin.db").path
        self.apiBaseUrl = Bundle.main.infoDictionary?["API_BASE_URL"] as? String ?? ""

        // API keys — injected from Secrets.xcconfig → Info.plist at build time
        self.geminiApiKey = Bundle.main.infoDictionary?["GEMINI_API_KEY"] as? String
        self.openWeatherApiKey = Bundle.main.infoDictionary?["OPENWEATHER_API_KEY"] as? String
        self.googleOAuthClientId = Bundle.main.infoDictionary?["GOOGLE_OAUTH_CLIENT_ID"] as? String

        if apiBaseUrl.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || apiBaseUrl.contains("$(") {
            errorMessage = "Missing API_BASE_URL. Fill it in Secrets.xcconfig for cloud sync."
        }
        if let key = geminiApiKey, !key.isEmpty, !key.contains("$(") {
            // ok
        } else {
            // Non-fatal: only affects Assistant feature.
            if errorMessage == nil {
                errorMessage = "Missing GEMINI_API_KEY. Fill it in Secrets.xcconfig to enable the AI assistant."
            }
        }
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
                googleOAuthClientId: googleOAuthClientId
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

                // If the cloud has a profile, the .NET engine should have seeded local SQLite during auth.
                // Hydrate the in-memory state so UI can route correctly.
                isHydratingAfterAuth = true
                await loadPatientProfile()
                isHydratingAfterAuth = false

                let _ = await performSync()
                return true
            } else {
                errorMessage = result.errorMessage ?? "Authentication failed"
                return false
            }
        } catch {
            errorMessage = "Authentication failed: \(error.localizedDescription)"
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
        medications = []
        chatMessages = []
        ocrDocuments = []
        medicalHistory = []
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

    func fetchCoachingAdvice() async {
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

    func saveOcrDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async {
        guard let engine else { return }
        do {
            _ = try await engine.saveOcrDocument(
                opaqueInternalName: opaqueInternalName,
                mimeType: mimeType,
                pageCount: pageCount,
                pageTexts: pageTexts
            )
        } catch {}
    }

    func sanitizeText(_ text: String) async -> String {
        guard let engine else { return text }
        return await engine.sanitizeText(text)
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

    func loadEnvironmentAdvice() async {
        guard let engine else { return }
        do { environmentAdvice = try await engine.getEnvironmentAdvice() } catch {}
    }

    // MARK: - Synchronization

    func performSync() async -> Bool {
        guard let engine else { return false }

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
        do { return try await engine.pushLocalChanges().success } catch { return false }
    }
}

