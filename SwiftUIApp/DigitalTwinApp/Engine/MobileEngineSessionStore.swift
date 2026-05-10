import Foundation
import SwiftUI
import Security
import GoogleSignIn

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
    @Published var cloudAccessToken: String?
    @Published var lastSyncCompletedAt: Date? // Notifies views that sync has completed

    // MARK: - Feature State

    @Published var medications: [MedicationInfo] = []
    @Published var medicationInteractions: [MedicationInteractionInfo] = []
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

    /// Exposed for services that need direct engine access (e.g. notification fetches). Nil until `initialize()` succeeds.
    internal var mobileEngineClient: MobileEngineClient? { engine }
    private let databasePath: String
    private let apiBaseUrl: String
    private let geminiApiKey: String?
    private let openRouterApiKey: String?
    private let openRouterModel: String?
    private let openWeatherApiKey: String?
    private let googleOAuthClientId: String?
    private var medicationInteractionRefreshTask: Task<Void, Never>?
    private var cacheWarmupTask: Task<Void, Never>?
    private var syncLoopTask: Task<Void, Never>?
    private var lastHealthKitSleepImportAt: Date?
    private var lastHealthKitSleepPulledThrough: Date?
    private var lastHealthKitVitalsImportAt: Date?
    private var lastHealthKitVitalsPulledThrough: Date?

    private let healthKitSleepImportCooldownSeconds: TimeInterval = 5 * 60
    private let healthKitSleepImportWindowDays = 14
    private let healthKitSleepIncrementalOverlapHours = 12
    private let healthKitVitalsImportCooldownSeconds: TimeInterval = 2 * 60
    private let healthKitVitalsImportWindowDays = 7
    private let healthKitVitalsIncrementalOverlapHours = 6

    private enum PersistedKeys {
        static let lastHealthKitSleepPulledThroughIso = "healthkit.sleep.lastPulledThrough.iso"
        static let lastHealthKitVitalsPulledThroughIso = "healthkit.vitals.lastPulledThrough.iso"
    }

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

        if let iso = UserDefaults.standard.string(forKey: PersistedKeys.lastHealthKitSleepPulledThroughIso),
           let date = ISO8601DateFormatter().date(from: iso) {
            lastHealthKitSleepPulledThrough = date
        }

        if let iso = UserDefaults.standard.string(forKey: PersistedKeys.lastHealthKitVitalsPulledThroughIso),
           let date = ISO8601DateFormatter().date(from: iso) {
            lastHealthKitVitalsPulledThrough = date
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
        medicationInteractionRefreshTask?.cancel()
        cacheWarmupTask?.cancel()
        syncLoopTask?.cancel()

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
                await restoreCachedSession()
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

    /// Restore a previously authenticated session from the local SQLite cache.
    /// Called automatically after initialize() — no Google sign-in needed if a user exists.
    private func restoreCachedSession() async {
        guard let engine else { return }
        do {
            if let user = try await engine.getCurrentUser() {
                currentUser = user
                // Load patient profile BEFORE setting isAuthenticated so ContentView
                // never renders ProfileSetupGateView (and never fires its .onAppear).
                patientProfile = try? await engine.getPatientProfile()
                isAuthenticated = true
                print("[CloudDebug][restoreCachedSession] user restored email=\(user.email) engineReady=true")

                // Google-only cloud auth:
                // - Cloud requests require a Google ID token (RS256) as Bearer.
                // - A cached local user does NOT imply we still have a Google token.
                // Try to restore the previous Google session and inject the ID token into the engine.
                if let restored = try? await GIDSignIn.sharedInstance.restorePreviousSignIn(),
                   let idToken = restored.idToken?.tokenString,
                   !idToken.isEmpty {
                    print("[CloudDebug][restoreCachedSession] restored Google ID token len=\(idToken.count)")
                    // `setCloudAccessToken` now effectively sets the bearer token used by the engine.
                    let setResult = try? await engine.setCloudAccessToken(idToken)
                    print("[CloudDebug][restoreCachedSession] engine.setCloudAccessToken success=\(setResult?.success ?? false) err=\(setResult?.error ?? "nil")")
                } else {
                    print("[CloudDebug][restoreCachedSession] no Google session to restore")
                }

                let status = (try? await engine.getCloudAuthStatus()) ?? false
                print("[CloudDebug][restoreCachedSession] engine.getCloudAuthStatus=\(status)")
                isCloudAuthenticated = status
                hasCloudProfile = status

                startPeriodicSync()
            }
        } catch {}
    }

    private func startPeriodicSync() {
        syncLoopTask?.cancel()
        syncLoopTask = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 7_000_000_000)
                guard let self = self, !Task.isCancelled else { break }
                print("[SyncLoop] Periodic sync tick")
                // Use isCloudAuthenticated (from .NET) instead of Swift's cloudAccessToken
                let _ = await self.performSync(waitForHealthKitImport: self.isCloudAuthenticated,
                                                skipCacheWarmup: true)
            }
        }
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

    /// Load the first-screen data set before dismissing the loading gate.
    func bootstrapAppDataForLaunch() async {
        guard isInitialized else { return }

        if isCloudAuthenticated {
            let reachable = (try? await engine?.isCloudReachable()) ?? false
            if !reachable {
                print("[CloudDebug] Cloud unreachable — proceeding with local data")
                await withTaskGroup(of: Void.self) { group in
                    group.addTask { await self.loadMedications(waitForInteractions: false) }
                    group.addTask { await self.loadSleepSessionsFromLocalStoreOnly() }
                    group.addTask { await self.loadLatestEnvironmentReading() }
                    group.addTask { await self.loadMedicalHistory() }
                    group.addTask { await self.loadOcrDocuments() }
                    group.addTask { await self.loadChatHistory() }
                }
                return
            }
        }

        // performSync already loads currentUser, patientProfile, and runs warmCachesAfterSyncInBackground
        // (which loads medications, sleep, and environment). Just call performSync once.
        let _ = await performSync(waitForHealthKitImport: true, skipCacheWarmup: false)

        // Only load items that performSync doesn't cover: medical history, OCR documents, and chat.
        // Everything else is already loaded by performSync and warmCachesAfterSyncInBackground.
        await withTaskGroup(of: Void.self) { group in
            group.addTask { await self.loadMedicalHistory() }
            group.addTask { await self.loadOcrDocuments() }
            group.addTask { await self.loadChatHistory() }
        }
    }

    func performManualSync() async -> Bool {
        let success = await backgroundSyncService.performManualSync()

        if success {
            await loadSleepSessions()
            warmCachesAfterSyncInBackground()
        }

        return success
    }

    // MARK: - Authentication

    func authenticate(googleIdToken: String) async -> Bool {
        guard let engine else { return false }

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            print("[CloudDebug][auth] starting google auth tokenLen=\(googleIdToken.count)")
            let result = try await engine.authenticate(googleIdToken: googleIdToken)
            if result.success {
                isAuthenticated = true
                currentUser = result.user
                hasCloudProfile = result.hasCloudProfile ?? false
                isCloudAuthenticated = !(result.accessToken?.isEmpty ?? true)
                cloudAccessToken = result.accessToken
                print("[CloudDebug][auth] success hasCloudProfile=\(hasCloudProfile) accessTokenLen=\(result.accessToken?.count ?? 0) isCloudAuthenticated=\(isCloudAuthenticated)")

                let status = (try? await engine.getCloudAuthStatus()) ?? false
                print("[CloudDebug][auth] engine.getCloudAuthStatus=\(status)")

                // If the cloud has a profile, the .NET engine should have seeded local SQLite during auth.
                // Hydrate the in-memory state so UI can route correctly.
                isHydratingAfterAuth = true
                await loadPatientProfile()
                isHydratingAfterAuth = false

                // Don't push on first-login/no-profile state; user may cancel profile setup.
                if hasCloudProfile {
                    let _ = await performSync()
                }
                return true
            } else {
                errorMessage = result.errorMessage ?? "Authentication failed"
                isCloudAuthenticated = false
                cloudAccessToken = nil
                print("[CloudDebug][auth] failed error=\(errorMessage ?? "nil")")
                return false
            }
        } catch {
            errorMessage = "Authentication failed: \(error.localizedDescription)"
            isCloudAuthenticated = false
            cloudAccessToken = nil
            print("[CloudDebug][auth] exception \(error.localizedDescription)")
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

    func updateUserProfile(_ update: UserUpdateInfo) async -> Bool {
        guard let engine else { return false }

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            let result = try await engine.updateCurrentUser(update)
            if result.success {
                await getCurrentUser()

                if isCloudAuthenticated {
                    let _ = await performSync()
                }

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
        medicationInteractionRefreshTask?.cancel()
        cacheWarmupTask?.cancel()
        syncLoopTask?.cancel()

        isAuthenticated = false
        currentUser = nil
        patientProfile = nil
        hasCloudProfile = false
        isCloudAuthenticated = false
        cloudAccessToken = nil
        medications = []
        sleepSessions = []
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

    func recordVitalSigns(_ vitalSigns: [VitalSignInput]) async -> Int {
        guard let engine else { return 0 }
        do { return try await engine.recordVitalSigns(vitalSigns).count } catch { return 0 }
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

    func loadMedications(waitForInteractions: Bool = false) async {
        guard let engine else { return }
        do {
            medications = try await engine.getMedications()
            if waitForInteractions {
                await refreshMedicationInteractionsCache()
            } else {
                refreshMedicationInteractionsCacheInBackground()
            }
        } catch {}
    }

    private func refreshMedicationInteractionsCacheInBackground() {
        medicationInteractionRefreshTask?.cancel()

        medicationInteractionRefreshTask = Task(priority: .utility) { [weak self] in
            guard let self else { return }
            await self.refreshMedicationInteractionsCache()
        }
    }

    /// Rebuild the interactions cache from the currently loaded medications.
    private func refreshMedicationInteractionsCache() async {
        let rxCuis = medications
            .filter { $0.isActive && $0.rxCui != nil }
            .compactMap { $0.rxCui }
        guard rxCuis.count >= 2 else {
            medicationInteractions = []
            return
        }
        medicationInteractions = await checkInteractions(rxCuis: rxCuis)
    }

    func addMedication(_ input: AddMedicationInput) async -> OperationResult {
        guard let engine else {
            return OperationResult(success: false, error: "Engine is not initialized")
        }

        do {
            let result = try await engine.addMedication(input)
            if result.success { await loadMedications() }
            return result
        } catch {
            errorMessage = "Failed to add medication: \(error.localizedDescription)"
            return OperationResult(success: false, error: error.localizedDescription)
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

    func refreshEnvironmentForPreferredLocation() async {
        if let manual = LocationManager.manualLocationCoordinatesIfSelected() {
            await fetchEnvironmentReading(latitude: manual.latitude, longitude: manual.longitude)
            return
        }

        if let current = LocationManager.cachedCurrentLocationCoordinates() {
            await fetchEnvironmentReading(latitude: current.latitude, longitude: current.longitude)
            return
        }

        await loadLatestEnvironmentReading()
    }

    func warmCachesAfterSyncInBackground() {
        cacheWarmupTask?.cancel()

        cacheWarmupTask = Task(priority: .utility) { [weak self] in
            guard let self else { return }
            // Load medications, sleep sessions, and environment in parallel
            async let med = self.loadMedications()
            async let sleep = self.loadSleepSessions()
            async let env = self.refreshEnvironmentForPreferredLocation()
            let _ = await (med, sleep, env)
        }
    }

    // MARK: - ECG

    func evaluateEcgFrame(samples: [Double], spO2: Double, heartRate: Double,
                          mlScores: [String: Double]? = nil) async -> EcgEvaluationResult? {
        guard let engine else { return nil }
        do {
            let leadCount = mlScores != nil ? 12 : 1
            let frame = EcgFrameInput(samples: samples, spO2: spO2, heartRate: heartRate,
                                      timestamp: Date(), mlScores: mlScores, numLeads: leadCount)
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
        let durationMinutes = Int(endTime.timeIntervalSince(startTime) / 60)
        let input = SleepSessionInput(startTime: startTime, endTime: endTime, durationMinutes: durationMinutes, qualityScore: qualityScore)
        return await recordSleepSession(input, reloadAfterWrite: true)
    }

    func recordSleepSession(_ input: SleepSessionInput, reloadAfterWrite: Bool) async -> Bool {
        guard let engine else { return false }

        do {
            let result = try await engine.recordSleepSession(input)
            if result.success, reloadAfterWrite {
                await loadSleepSessions()
            }
            return result.success
        } catch {
            return false
        }
    }

    func loadSleepSessions(from: Date? = nil, to: Date? = nil) async {
        guard let engine else { return }

        func formatDate(_ value: Date?) -> String {
            guard let value else { return "nil" }
            return ISO8601DateFormatter().string(from: value)
        }

        _ = await importHealthKitSleepIfNeeded(reason: "loadSleepSessions", force: false)

        do {
            sleepSessions = try await engine.getSleepSessions(from: from, to: to)
        } catch {

        }
    }

    /// Read sleep sessions from local SQLite without triggering a HealthKit import.
    /// Use this for startup/home rendering so UI doesn't block on HealthKit.
    func loadSleepSessionsFromLocalStoreOnly(from: Date? = nil, to: Date? = nil) async {
        guard let engine else { return }
        do {
            sleepSessions = try await engine.getSleepSessions(from: from, to: to)
        } catch {
        }
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
        if advice.isDeterministicFallback { return false }
        return Date().timeIntervalSince(advice.timestamp) < maxAgeSeconds
    }

    // MARK: - Synchronization

    func performSync(waitForHealthKitImport: Bool = false, skipCacheWarmup: Bool = false) async -> Bool {
        guard let engine else { return false }

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            print("[CloudDebug][performSync] starting isCloudAuthenticated(before)=\(isCloudAuthenticated) tokenLen=\(cloudAccessToken?.count ?? 0)")
            if healthKitService.isAuthorized {
                if waitForHealthKitImport {
                    await syncHealthKitData(reason: "performSync-cloud")
                } else {
                    // Run HealthKit import in background; cloud sync can proceed immediately.
                    Task(priority: .utility) { [weak self] in
                        await self?.syncHealthKitData(reason: "performSync-cloud")
                    }
                }
            }

            let result = try await engine.performSync()

            if result.success {
                // Ask the engine whether cloud auth is actually ready (token store is in-memory).
                // Sync can report success in local-only mode.
                let status = (try? await engine.getCloudAuthStatus()) ?? false
                print("[CloudDebug][performSync] engine.performSync success; engine.getCloudAuthStatus=\(status)")
                isCloudAuthenticated = status
                hasCloudProfile = status
                if healthKitService.isAuthorized {
                    await writeVitalsToHealthKit()
                }

                await getCurrentUser()
                await loadPatientProfile()
                if !skipCacheWarmup {
                    warmCachesAfterSyncInBackground()
                }
                
                // Notify views that sync completed so they can refresh
                lastSyncCompletedAt = Date()
                return true
            } else {
                // If cloud isn't configured or token is missing/expired, treat as local-only.
                // Keep the UI responsive, but correctly reflect cloud unavailability.
                let err = (result.error ?? "Sync failed").lowercased()
                print("[CloudDebug][performSync] engine.performSync failed err=\(result.error ?? "nil")")
                if err.contains("not authenticated") || err.contains("not authorized") || err.contains("unauthorized") {
                    isCloudAuthenticated = false
                    cloudAccessToken = nil
                }

                await getCurrentUser()
                await loadPatientProfile()
                if !skipCacheWarmup {
                    warmCachesAfterSyncInBackground()
                }
                lastSyncCompletedAt = Date()
                return false
            }
        } catch {
            let msg = error.localizedDescription.lowercased()
            print("[CloudDebug][performSync] exception \(error.localizedDescription)")
            if msg.contains("not authenticated") || msg.contains("unauthorized") {
                isCloudAuthenticated = false
                cloudAccessToken = nil
            }

            await getCurrentUser()
            await loadPatientProfile()
            lastSyncCompletedAt = Date()
            return false
        }
    }

    private func syncHealthKitData(reason: String) async {
        do {
            if let lastImport = lastHealthKitVitalsImportAt,
               Date().timeIntervalSince(lastImport) < healthKitVitalsImportCooldownSeconds {
                return
            }

            let endDate = Date()
            let windowFloor = Calendar.current.date(byAdding: .day, value: -healthKitVitalsImportWindowDays, to: endDate) ?? endDate
            let incrementalStart: Date? = {
                guard let lastPulledThrough = lastHealthKitVitalsPulledThrough else { return nil }
                let overlap = TimeInterval(healthKitVitalsIncrementalOverlapHours) * 60 * 60
                return lastPulledThrough.addingTimeInterval(-overlap)
            }()

            let startDate = max(windowFloor, incrementalStart ?? windowFloor)

            let healthKitVitals = try await healthKitService.readVitalSigns(from: startDate, to: endDate)

            let vitalInputs = healthKitVitals.map { vital in
                VitalSignInput(
                    type: vital.type,
                    value: vital.value,
                    unit: vital.unit,
                    source: vital.source,
                    timestamp: vital.timestamp
                )
            }

            let recordedCount = await recordVitalSigns(vitalInputs)

            let estimatedSleepInserted = await importHealthKitSleepIfNeeded(reason: "syncHealthKitData", force: true)

            lastHealthKitVitalsImportAt = Date()
            lastHealthKitVitalsPulledThrough = endDate
            UserDefaults.standard.set(
                ISO8601DateFormatter().string(from: endDate),
                forKey: PersistedKeys.lastHealthKitVitalsPulledThroughIso
            )

        } catch {

        }
    }

    private func importHealthKitSleepIfNeeded(reason: String, force: Bool) async -> Int {
        guard healthKitService.isAuthorized else {
            return 0
        }

        if !force,
           let lastImport = lastHealthKitSleepImportAt,
           Date().timeIntervalSince(lastImport) < healthKitSleepImportCooldownSeconds {
            return 0
        }

        guard let engine else {
            return 0
        }

        do {
            let endDate = Date()
            // Incremental pull: start from the last successful pull-through timestamp (persisted),
            // with a small overlap to tolerate HealthKit late writes / timezone boundary issues.
            // Safety cap: never go farther back than the max window.
            let windowFloor = Calendar.current.date(byAdding: .day, value: -healthKitSleepImportWindowDays, to: endDate) ?? endDate
            let incrementalStart: Date? = {
                guard let lastPulledThrough = lastHealthKitSleepPulledThrough else { return nil }
                let overlap = TimeInterval(healthKitSleepIncrementalOverlapHours) * 60 * 60
                return lastPulledThrough.addingTimeInterval(-overlap)
            }()

            let startDate = max(windowFloor, incrementalStart ?? windowFloor)

            let beforeCount = (try? await engine.getSleepSessions(from: nil, to: nil).count) ?? -1
            let sessions = try await healthKitService.readSleepSessions(from: startDate, to: endDate)

            var successfulWrites = 0
            for session in sessions {
                let normalized = SleepSessionInput(
                    startTime: session.startTime,
                    endTime: session.endTime,
                    durationMinutes: session.durationMinutes,
                    qualityScore: session.qualityScore ?? 0
                )

                do {
                    let result = try await engine.recordSleepSession(normalized)
                    if result.success {
                        successfulWrites += 1
                    }
                } catch {
                }
            }

            let afterCount = (try? await engine.getSleepSessions(from: nil, to: nil).count) ?? -1
            let estimatedInserted = max(afterCount - beforeCount, 0)

            print("[SleepDebug][HealthKitSync] sleep import write summary reason=\(reason) successfulWrites=\(successfulWrites) localCountBefore=\(beforeCount) localCountAfter=\(afterCount) estimatedInserted=\(estimatedInserted)")

            lastHealthKitSleepImportAt = Date()
            lastHealthKitSleepPulledThrough = endDate
            UserDefaults.standard.set(ISO8601DateFormatter().string(from: endDate), forKey: PersistedKeys.lastHealthKitSleepPulledThroughIso)
            return estimatedInserted
        } catch {
            print("[SleepDebug][HealthKitSync] sleep import failed. reason=\(reason) error=\(error.localizedDescription)")
            return 0
        }
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

