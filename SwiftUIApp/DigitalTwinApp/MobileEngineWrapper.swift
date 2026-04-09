import SwiftUI
import Foundation

/// Swift wrapper around the .NET Mobile Engine
/// Provides a clean Swift interface to the embedded .NET backend
@MainActor
class MobileEngineWrapper: ObservableObject {
    
    // MARK: - Published State
    
    @Published var isInitialized = false
    @Published var isAuthenticated = false
    @Published var currentUser: UserInfo?
    @Published var patientProfile: PatientInfo?
    @Published var isLoading = false
    @Published var errorMessage: String?
    
    // MARK: - New Feature State
    
    @Published var medications: [MedicationInfo] = []
    @Published var latestEnvironmentReading: EnvironmentReadingInfo?
    @Published var chatMessages: [ChatMessageInfo] = []
    @Published var coachingAdvice: CoachingAdviceInfo?
    @Published var sleepSessions: [SleepSessionInfo] = []
    @Published var medicalHistory: [MedicalHistoryEntryInfo] = []
    @Published var ocrDocuments: [OcrDocumentInfo] = []
    
    // MARK: - Native Services
    
    @Published var healthKitService = HealthKitService()
    @Published var backgroundSyncService = BackgroundSyncService.shared
    @Published var biometricAuthService = BiometricAuthService()
    
    // MARK: - Private Properties
    
    private var engine: MobileEngineHandle?
    private let databasePath: String
    private let apiBaseUrl: String
    private let geminiApiKey: String?
    private let openWeatherApiKey: String?
    private let googleOAuthClientId: String?
    
    // MARK: - Initialization
    
    init() {
        let documentsPath = FileManager.default.urls(for: .documentDirectory, 
                                                   in: .userDomainMask).first!
        self.databasePath = documentsPath.appendingPathComponent("digitaltwin.db").path
        self.apiBaseUrl = Bundle.main.infoDictionary?["API_BASE_URL"] as? String ?? ""
        
        // API keys — injected from Secrets.xcconfig → Info.plist at build time
        self.geminiApiKey = Bundle.main.infoDictionary?["GEMINI_API_KEY"] as? String
        self.openWeatherApiKey = Bundle.main.infoDictionary?["OPENWEATHER_API_KEY"] as? String
        self.googleOAuthClientId = Bundle.main.infoDictionary?["GOOGLE_OAUTH_CLIENT_ID"] as? String
    }

    deinit {
        // We can't `await` in deinit; fire-and-forget cleanup.
        if let engine = engine {
            Task { await engine.dispose() }
        }
    }
    
    /// Initialize the .NET engine and native services
    func initialize() async {
        guard !isInitialized else { return }
        
        do {
            // Initialize .NET engine
            engine = try MobileEngineHandle(databasePath: databasePath, apiBaseUrl: apiBaseUrl, geminiApiKey: geminiApiKey, openWeatherApiKey: openWeatherApiKey, googleOAuthClientId: googleOAuthClientId)
            
            // Initialize database
            let initResult = try await engine?.initializeDatabase()
            if let result = initResult, result.success {
                isInitialized = true
                print("[MobileEngineWrapper] Engine initialized successfully")
                
                // Initialize native services
                await initializeNativeServices()
                
            } else {
                throw EngineError.initializationFailed(initResult?.error ?? "Unknown error")
            }
        } catch {
            print("[MobileEngineWrapper] Initialization failed: \(error)")
            errorMessage = "Failed to initialize app: \(error.localizedDescription)"
        }
    }
    
    /// Initialize native iOS services
    private func initializeNativeServices() async {
        // Initialize HealthKit service
        healthKitService.refreshAuthorizationStatus()
        
        // Initialize biometric authentication service
        biometricAuthService.checkBiometricAvailability()
        
        // Load background sync preferences
        backgroundSyncService.loadBackgroundSyncPreference()
        
        print("[MobileEngineWrapper] Native services initialized")
    }

    // MARK: - Native Services Integration
    
    /// Request HealthKit authorization
    func requestHealthKitAuthorization() async -> Bool {
        do {
            try await healthKitService.requestAuthorization()
            return healthKitService.isAuthorized
        } catch {
            print("[MobileEngineWrapper] HealthKit authorization failed: \(error)")
            errorMessage = "HealthKit authorization failed: \(error.localizedDescription)"
            return false
        }
    }
    
    /// Enable biometric authentication
    func enableBiometricAuth() async -> Bool {
        let success = await biometricAuthService.enableBiometricAuth()
        if !success {
            errorMessage = "Failed to enable biometric authentication"
        }
        return success
    }
    
    /// Authenticate with biometrics
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
    
    /// Enable background sync
    func enableBackgroundSync(_ enabled: Bool) {
        backgroundSyncService.setBackgroundSyncEnabled(enabled)
    }
    
    /// Perform manual background sync
    func performManualSync() async -> Bool {
        return await backgroundSyncService.performManualSync()
    }
    
    // MARK: - Authentication
    
    /// Authenticate with Google ID token
    func authenticate(googleIdToken: String) async -> Bool {
        guard let engine = engine else { return false }
        
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        
        do {
            let result = try await engine.authenticate(googleIdToken: googleIdToken)
            
            if result.success {
                isAuthenticated = true
                currentUser = result.user
                
                // Load patient profile after authentication
                await loadPatientProfile()
                
                // Trigger initial cloud sync now that we're authenticated
                let _ = await performSync()
                
                print("[MobileEngineWrapper] Authentication successful")
                return true
            } else {
                errorMessage = result.errorMessage ?? "Authentication failed"
                return false
            }
        } catch {
            print("[MobileEngineWrapper] Authentication error: \(error)")
            errorMessage = "Authentication failed: \(error.localizedDescription)"
            return false
        }
    }
    
    /// Get current user
    func getCurrentUser() async {
        guard let engine = engine else { return }
        
        do {
            currentUser = try await engine.getCurrentUser()
            isAuthenticated = currentUser != nil
        } catch {
            print("[MobileEngineWrapper] Failed to get current user: \(error)")
        }
    }
    
    // MARK: - Patient Profile
    
    /// Load patient profile
    func loadPatientProfile() async {
        guard let engine = engine else { return }
        
        do {
            patientProfile = try await engine.getPatientProfile()
        } catch {
            print("[MobileEngineWrapper] Failed to load patient profile: \(error)")
        }
    }
    
    /// Update patient profile
    func updatePatientProfile(_ update: PatientUpdateInfo) async -> Bool {
        guard let engine = engine else { return false }
        
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        
        do {
            let result = try await engine.updatePatientProfile(update)
            
            if result.success {
                await loadPatientProfile() // Reload after update
                return true
            } else {
                errorMessage = result.error ?? "Update failed"
                return false
            }
        } catch {
            print("[MobileEngineWrapper] Failed to update patient profile: \(error)")
            errorMessage = "Update failed: \(error.localizedDescription)"
            return false
        }
    }
    
    // MARK: - Sign Out
    
    func signOut() async {
        isAuthenticated = false
        currentUser = nil
        patientProfile = nil
        medications = []
        chatMessages = []
        ocrDocuments = []
        medicalHistory = []
    }
    
    // MARK: - Latest Vitals (convenience)
    
    struct LatestVitals {
        var heartRate: Int?
        var oxygenSaturation: Int?
        var stepCount: Int?
    }
    
    var latestVitals: LatestVitals? {
        // This is a computed convenience; real implementation fetches from cache
        nil
    }
    
    // MARK: - Vital Signs
    
    /// Record a vital sign
    func recordVitalSign(_ vitalSign: VitalSignInput) async -> Bool {
        guard let engine = engine else { return false }
        
        do {
            let result = try await engine.recordVitalSign(vitalSign)
            return result.success
        } catch {
            print("[MobileEngineWrapper] Failed to record vital sign: \(error)")
            return false
        }
    }
    
    /// Get vital signs for date range
    func getVitalSigns(from: Date? = nil, to: Date? = nil) async -> [VitalSignInfo] {
        guard let engine = engine else { return [] }
        
        do {
            return try await engine.getVitalSigns(from: from, to: to)
        } catch {
            print("[MobileEngineWrapper] Failed to get vital signs: \(error)")
            return []
        }
    }
    
    /// Get vital signs by type
    func getVitalSignsByType(_ type: VitalSignType, from: Date? = nil, to: Date? = nil) async -> [VitalSignInfo] {
        guard let engine = engine else { return [] }
        
        do {
            return try await engine.getVitalSignsByType(type, from: from, to: to)
        } catch {
            print("[MobileEngineWrapper] Failed to get vital signs by type: \(error)")
            return []
        }
    }
    
    // MARK: - Medications
    
    /// Load all medications
    func loadMedications() async {
        guard let engine = engine else { return }
        do {
            medications = try await engine.getMedications()
        } catch {
            print("[MobileEngineWrapper] Failed to load medications: \(error)")
        }
    }
    
    /// Add a new medication
    func addMedication(_ input: AddMedicationInput) async -> Bool {
        guard let engine = engine else { return false }
        do {
            let result = try await engine.addMedication(input)
            if result.success { await loadMedications() }
            return result.success
        } catch {
            print("[MobileEngineWrapper] Failed to add medication: \(error)")
            errorMessage = "Failed to add medication: \(error.localizedDescription)"
            return false
        }
    }
    
    /// Discontinue a medication
    func discontinueMedication(id: UUID, reason: String?) async -> Bool {
        guard let engine = engine else { return false }
        do {
            let input = DiscontinueMedicationInput(medicationId: id, reason: reason)
            let result = try await engine.discontinueMedication(input)
            if result.success { await loadMedications() }
            return result.success
        } catch {
            print("[MobileEngineWrapper] Failed to discontinue medication: \(error)")
            return false
        }
    }
    
    /// Search drugs by name
    func searchDrugs(query: String) async -> [DrugSearchResult] {
        guard let engine = engine else { return [] }
        do {
            return try await engine.searchDrugs(query: query)
        } catch {
            print("[MobileEngineWrapper] Failed to search drugs: \(error)")
            return []
        }
    }
    
    /// Check interactions between medications
    func checkInteractions(rxCuis: [String]) async -> [MedicationInteractionInfo] {
        guard let engine = engine else { return [] }
        do {
            return try await engine.checkInteractions(rxCuis: rxCuis)
        } catch {
            print("[MobileEngineWrapper] Failed to check interactions: \(error)")
            return []
        }
    }
    
    // MARK: - Environment
    
    /// Fetch current environment data for location
    func fetchEnvironmentReading(latitude: Double, longitude: Double) async {
        guard let engine = engine else { return }
        do {
            latestEnvironmentReading = try await engine.getEnvironmentReading(latitude: latitude, longitude: longitude)
        } catch {
            print("[MobileEngineWrapper] Failed to get environment reading: \(error)")
        }
    }
    
    /// Load latest cached environment reading
    func loadLatestEnvironmentReading() async {
        guard let engine = engine else { return }
        do {
            latestEnvironmentReading = try await engine.getLatestEnvironmentReading()
        } catch {
            print("[MobileEngineWrapper] Failed to get latest environment reading: \(error)")
        }
    }
    
    // MARK: - ECG
    
    /// Evaluate an ECG frame
    func evaluateEcgFrame(samples: [Double], spO2: Double, heartRate: Double) async -> EcgEvaluationResult? {
        guard let engine = engine else { return nil }
        do {
            let frame = EcgFrameInput(samples: samples, spO2: spO2, heartRate: heartRate, timestamp: Date())
            return try await engine.evaluateEcgFrame(frame)
        } catch {
            print("[MobileEngineWrapper] Failed to evaluate ECG frame: \(error)")
            return nil
        }
    }
    
    // MARK: - AI Chat
    
    /// Send a message to the AI assistant
    func sendChatMessage(_ message: String) async -> Bool {
        guard let engine = engine else { return false }
        do {
            let response = try await engine.sendChatMessage(message)
            // Add user message and AI response to local state
            await loadChatHistory()
            return true
        } catch {
            print("[MobileEngineWrapper] Failed to send chat message: \(error)")
            errorMessage = "Failed to send message: \(error.localizedDescription)"
            return false
        }
    }
    
    /// Load chat history
    func loadChatHistory() async {
        guard let engine = engine else { return }
        do {
            chatMessages = try await engine.getChatHistory()
        } catch {
            print("[MobileEngineWrapper] Failed to load chat history: \(error)")
        }
    }
    
    /// Clear chat history
    func clearChatHistory() async -> Bool {
        guard let engine = engine else { return false }
        do {
            let result = try await engine.clearChatHistory()
            if result.success { chatMessages = [] }
            return result.success
        } catch {
            print("[MobileEngineWrapper] Failed to clear chat history: \(error)")
            return false
        }
    }
    
    // MARK: - Coaching
    
    /// Get AI coaching advice
    func fetchCoachingAdvice() async {
        guard let engine = engine else { return }
        do {
            coachingAdvice = try await engine.getCoachingAdvice()
        } catch {
            print("[MobileEngineWrapper] Failed to get coaching advice: \(error)")
        }
    }
    
    // MARK: - Sleep
    
    /// Record a sleep session
    func recordSleepSession(startTime: Date, endTime: Date, qualityScore: Double?) async -> Bool {
        guard let engine = engine else { return false }
        let durationMinutes = Int(endTime.timeIntervalSince(startTime) / 60)
        let input = SleepSessionInput(startTime: startTime, endTime: endTime, durationMinutes: durationMinutes, qualityScore: qualityScore)
        do {
            let result = try await engine.recordSleepSession(input)
            if result.success { await loadSleepSessions() }
            return result.success
        } catch {
            print("[MobileEngineWrapper] Failed to record sleep session: \(error)")
            return false
        }
    }
    
    /// Load sleep sessions
    func loadSleepSessions(from: Date? = nil, to: Date? = nil) async {
        guard let engine = engine else { return }
        do {
            sleepSessions = try await engine.getSleepSessions(from: from, to: to)
        } catch {
            print("[MobileEngineWrapper] Failed to load sleep sessions: \(error)")
        }
    }
    
    // MARK: - Medical History & OCR
    
    /// Load medical history
    func loadMedicalHistory() async {
        guard let engine = engine else { return }
        do {
            medicalHistory = try await engine.getMedicalHistory()
        } catch {
            print("[MobileEngineWrapper] Failed to load medical history: \(error)")
        }
    }
    
    /// Load OCR documents
    func loadOcrDocuments() async {
        guard let engine = engine else { return }
        do {
            ocrDocuments = try await engine.getOcrDocuments()
        } catch {
            print("[MobileEngineWrapper] Failed to load OCR documents: \(error)")
        }
    }
    
    // MARK: - OCR Text Processing
    
    /// Classify document type from OCR text
    func classifyDocument(_ ocrText: String) async -> String {
        guard let engine = engine else { return "Unknown" }
        return await engine.classifyDocument(ocrText)
    }
    
    /// Full OCR processing pipeline
    func processFullOcr(_ ocrText: String) async -> OcrProcessingResult? {
        guard let engine = engine else { return nil }
        do {
            return try await engine.processFullOcr(ocrText)
        } catch {
            print("[MobileEngineWrapper] Failed to process OCR: \(error)")
            return nil
        }
    }
    
    /// Save OCR document and extract medical history
    func saveOcrDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) async {
        guard let engine = engine else { return }
        do {
            _ = try await engine.saveOcrDocument(
                opaqueInternalName: opaqueInternalName,
                mimeType: mimeType,
                pageCount: pageCount,
                pageTexts: pageTexts
            )
        } catch {
            print("[MobileEngineWrapper] Failed to save OCR document: \(error)")
        }
    }
    
    /// Sanitize text (redact PII)
    func sanitizeText(_ text: String) async -> String {
        guard let engine = engine else { return text }
        return await engine.sanitizeText(text)
    }
    
    // MARK: - Synchronization
    
    /// Perform full sync with cloud (includes HealthKit integration)
    func performSync() async -> Bool {
        guard let engine = engine else { return false }
        
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        
        do {
            // 1. Sync HealthKit data if authorized
            if healthKitService.isAuthorized {
                await syncHealthKitData()
            }
            
            // 2. Perform engine sync
            let result = try await engine.performSync()
            
            if result.success {
                // 3. Write new vitals back to HealthKit if authorized
                if healthKitService.isAuthorized {
                    await writeVitalsToHealthKit()
                }
                
                // 4. Reload data after sync
                await getCurrentUser()
                await loadPatientProfile()
                
                return true
            } else {
                errorMessage = result.error ?? "Sync failed"
                return false
            }
        } catch {
            print("[MobileEngineWrapper] Sync failed: \(error)")
            errorMessage = "Sync failed: \(error.localizedDescription)"
            return false
        }
    }
    
    /// Sync HealthKit data to the engine
    private func syncHealthKitData() async {
        do {
            // Get HealthKit data from the last 7 days
            let endDate = Date()
            let startDate = Calendar.current.date(byAdding: .day, value: -7, to: endDate) ?? endDate
            
            let healthKitVitals = try await healthKitService.readVitalSigns(from: startDate, to: endDate)
            
            // Record each vital sign in the engine
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
            
            print("[MobileEngineWrapper] Synced \(healthKitVitals.count) HealthKit vitals")
            
        } catch {
            print("[MobileEngineWrapper] HealthKit sync error: \(error)")
        }
    }
    
    /// Write new vitals back to HealthKit
    private func writeVitalsToHealthKit() async {
        do {
            // Get recent unsynced vitals from engine
            let endDate = Date()
            let startDate = Calendar.current.date(byAdding: .hour, value: -1, to: endDate) ?? endDate
            
            let recentVitals = await getVitalSigns(from: startDate, to: endDate)
            let unsyncedVitals = recentVitals.filter { !$0.isSynced && $0.source != "HealthKit" }
            
            // Convert to VitalSignInput and write to HealthKit
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
                print("[MobileEngineWrapper] Wrote \(vitalInputs.count) vitals to HealthKit")
            }
            
        } catch {
            print("[MobileEngineWrapper] HealthKit write error: \(error)")
        }
    }
    
    /// Push local changes to cloud
    func pushLocalChanges() async -> Bool {
        guard let engine = engine else { return false }
        
        do {
            let result = try await engine.pushLocalChanges()
            return result.success
        } catch {
            print("[MobileEngineWrapper] Failed to push local changes: \(error)")
            return false
        }
    }
}

// MARK: - Engine Errors

enum EngineError: LocalizedError {
    case initializationFailed(String)
    case engineNotInitialized
    case invalidResponse
    
    var errorDescription: String? {
        switch self {
        case .initializationFailed(let message):
            return "Engine initialization failed: \(message)"
        case .engineNotInitialized:
            return "Engine not initialized"
        case .invalidResponse:
            return "Invalid response from engine"
        }
    }
}

// MARK: - Data Models

struct UserInfo: Codable, Identifiable {
    let id: UUID
    let email: String
    let firstName: String?
    let lastName: String?
    let photoUrl: String?
    
    var displayName: String {
        if let firstName = firstName, let lastName = lastName {
            return "\(firstName) \(lastName)"
        } else if let firstName = firstName {
            return firstName
        } else {
            return email
        }
    }
}

struct PatientInfo: Codable, Identifiable {
    let id: UUID
    let userId: UUID
    let bloodType: String?
    let allergies: String?
    let medicalHistoryNotes: String?
    let weight: Double?
    let height: Double?
    let bloodPressureSystolic: Int?
    let bloodPressureDiastolic: Int?
    let cholesterol: Double?
    let cnp: String?
    let isSynced: Bool
}

struct PatientUpdateInfo: Codable {
    let bloodType: String?
    let allergies: String?
    let medicalHistoryNotes: String?
    let weight: Double?
    let height: Double?
    let bloodPressureSystolic: Int?
    let bloodPressureDiastolic: Int?
    let cholesterol: Double?
    let cnp: String?
}

struct VitalSignInput: Codable {
    let type: VitalSignType
    let value: Double
    let unit: String
    let source: String
    let timestamp: Date?
}

struct VitalSignInfo: Codable, Identifiable {
    let id: UUID
    let type: VitalSignType
    let value: Double
    let unit: String
    let source: String
    let timestamp: Date
    let isSynced: Bool
}

enum VitalSignType: Int, Codable, CaseIterable {
    case heartRate = 0
    case bloodPressure = 1
    case temperature = 2
    case oxygenSaturation = 3
    case respiratoryRate = 4
    case bloodGlucose = 5
    case weight = 6
    case height = 7
    case bmi = 8
    case stepCount = 9
    case caloriesBurned = 10
    case sleepDuration = 11
    
    var displayName: String {
        switch self {
        case .heartRate: return "Heart Rate"
        case .bloodPressure: return "Blood Pressure"
        case .temperature: return "Temperature"
        case .oxygenSaturation: return "Oxygen Saturation"
        case .respiratoryRate: return "Respiratory Rate"
        case .bloodGlucose: return "Blood Glucose"
        case .weight: return "Weight"
        case .height: return "Height"
        case .bmi: return "BMI"
        case .stepCount: return "Step Count"
        case .caloriesBurned: return "Calories Burned"
        case .sleepDuration: return "Sleep Duration"
        }
    }
    
    var unit: String {
        switch self {
        case .heartRate: return "bpm"
        case .bloodPressure: return "mmHg"
        case .temperature: return "°F"
        case .oxygenSaturation: return "%"
        case .respiratoryRate: return "breaths/min"
        case .bloodGlucose: return "mg/dL"
        case .weight: return "lbs"
        case .height: return "in"
        case .bmi: return ""
        case .stepCount: return "steps"
        case .caloriesBurned: return "cal"
        case .sleepDuration: return "hours"
        }
    }
}

// MARK: - Response Models

struct AuthenticationResult: Codable {
    let success: Bool
    let errorMessage: String?
    let accessToken: String?
    let user: UserInfo?
}

struct OperationResult: Codable {
    let success: Bool
    let error: String?
}

// MARK: - .NET Engine Handle

/// Swift handle to the .NET Mobile Engine via C bridge.
/// Implemented as an actor to satisfy Swift 6 concurrency checks.
actor MobileEngineHandle {
    private let bridge = DotNetBridge()
    
    init(databasePath: String, apiBaseUrl: String, geminiApiKey: String? = nil, openWeatherApiKey: String? = nil, googleOAuthClientId: String? = nil) throws {
        let result = try bridge.initialize(databasePath: databasePath, apiBaseUrl: apiBaseUrl, geminiApiKey: geminiApiKey, openWeatherApiKey: openWeatherApiKey, googleOAuthClientId: googleOAuthClientId)
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