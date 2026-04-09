import Foundation

/// Swift bridge to the .NET Mobile Engine
/// Provides Swift-friendly interface to the embedded .NET backend
class DotNetBridge {
    
    // MARK: - C Function Declarations
    
    @_silgen_name("mobile_engine_initialize")
    private static func mobile_engine_initialize(_ databasePath: UnsafePointer<CChar>, _ apiBaseUrl: UnsafePointer<CChar>, _ geminiApiKey: UnsafePointer<CChar>?, _ openWeatherApiKey: UnsafePointer<CChar>?, _ googleOAuthClientId: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_initialize_database")
    private static func mobile_engine_initialize_database() -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_dispose")
    private static func mobile_engine_dispose()
    
    // Authentication
    @_silgen_name("mobile_engine_authenticate")
    private static func mobile_engine_authenticate(_ googleIdToken: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_current_user")
    private static func mobile_engine_get_current_user() -> UnsafePointer<CChar>?
    
    // Patient Profile
    @_silgen_name("mobile_engine_get_patient_profile")
    private static func mobile_engine_get_patient_profile() -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_update_patient_profile")
    private static func mobile_engine_update_patient_profile(_ updateJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    // Vital Signs
    @_silgen_name("mobile_engine_record_vital_sign")
    private static func mobile_engine_record_vital_sign(_ vitalSignJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_record_vital_signs")
    private static func mobile_engine_record_vital_signs(_ vitalSignsJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_vital_signs")
    private static func mobile_engine_get_vital_signs(_ fromDateIso: UnsafePointer<CChar>?, _ toDateIso: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_vital_signs_by_type")
    private static func mobile_engine_get_vital_signs_by_type(_ vitalType: Int32, _ fromDateIso: UnsafePointer<CChar>?, _ toDateIso: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    // Synchronization
    @_silgen_name("mobile_engine_perform_sync")
    private static func mobile_engine_perform_sync() -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_push_local_changes")
    private static func mobile_engine_push_local_changes() -> UnsafePointer<CChar>?
    
    // Memory Management
    @_silgen_name("mobile_engine_free_string")
    private static func mobile_engine_free_string(_ ptr: UnsafePointer<CChar>?)
    
    // Medications
    @_silgen_name("mobile_engine_get_medications")
    private static func mobile_engine_get_medications() -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_add_medication")
    private static func mobile_engine_add_medication(_ inputJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_discontinue_medication")
    private static func mobile_engine_discontinue_medication(_ inputJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_search_drugs")
    private static func mobile_engine_search_drugs(_ query: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_check_interactions")
    private static func mobile_engine_check_interactions(_ rxCuisJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    // Environment
    @_silgen_name("mobile_engine_get_environment_reading")
    private static func mobile_engine_get_environment_reading(_ latitude: Double, _ longitude: Double) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_latest_environment_reading")
    private static func mobile_engine_get_latest_environment_reading() -> UnsafePointer<CChar>?
    
    // ECG
    @_silgen_name("mobile_engine_evaluate_ecg_frame")
    private static func mobile_engine_evaluate_ecg_frame(_ frameJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    // AI Chat
    @_silgen_name("mobile_engine_send_chat_message")
    private static func mobile_engine_send_chat_message(_ message: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_chat_history")
    private static func mobile_engine_get_chat_history() -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_clear_chat_history")
    private static func mobile_engine_clear_chat_history() -> UnsafePointer<CChar>?
    
    // Coaching
    @_silgen_name("mobile_engine_get_coaching_advice")
    private static func mobile_engine_get_coaching_advice() -> UnsafePointer<CChar>?
    
    // Sleep
    @_silgen_name("mobile_engine_record_sleep_session")
    private static func mobile_engine_record_sleep_session(_ sessionJson: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_sleep_sessions")
    private static func mobile_engine_get_sleep_sessions(_ fromDateIso: UnsafePointer<CChar>?, _ toDateIso: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    // Medical History & OCR
    @_silgen_name("mobile_engine_get_medical_history")
    private static func mobile_engine_get_medical_history() -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_get_ocr_documents")
    private static func mobile_engine_get_ocr_documents() -> UnsafePointer<CChar>?
    
    // OCR Text Processing
    @_silgen_name("mobile_engine_classify_document")
    private static func mobile_engine_classify_document(_ ocrText: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_extract_identity")
    private static func mobile_engine_extract_identity(_ ocrText: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_validate_identity")
    private static func mobile_engine_validate_identity(_ ocrText: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_sanitize_text")
    private static func mobile_engine_sanitize_text(_ ocrText: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_extract_structured")
    private static func mobile_engine_extract_structured(_ ocrText: UnsafePointer<CChar>?, _ docType: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_process_full_ocr")
    private static func mobile_engine_process_full_ocr(_ ocrText: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    @_silgen_name("mobile_engine_save_ocr_document")
    private static func mobile_engine_save_ocr_document(_ inputJson: UnsafePointer<CChar>?) -> UnsafePointer<CChar>?
    
    // MARK: - Public Interface
    
    private let jsonDecoder = JSONDecoder()
    private let jsonEncoder = JSONEncoder()
    
    init() {
        // Configure JSON coding
        jsonDecoder.dateDecodingStrategy = .iso8601
        jsonEncoder.dateEncodingStrategy = .iso8601
    }
    
    // MARK: - Lifecycle Management
    
    /// Initialize the .NET engine
    func initialize(databasePath: String, apiBaseUrl: String, geminiApiKey: String? = nil, openWeatherApiKey: String? = nil, googleOAuthClientId: String? = nil) throws -> OperationResult {
        // Use helper to convert optionals — avoids combinatorial withCString nesting
        func withOptionalCString(_ str: String?, _ body: (UnsafePointer<CChar>?) -> UnsafePointer<CChar>?) -> UnsafePointer<CChar>? {
            if let s = str { return s.withCString { body($0) } }
            return body(nil)
        }
        
        let result = databasePath.withCString { dbPathPtr in
            apiBaseUrl.withCString { apiUrlPtr in
                withOptionalCString(geminiApiKey) { geminiPtr in
                    withOptionalCString(openWeatherApiKey) { weatherPtr in
                        withOptionalCString(googleOAuthClientId) { googlePtr in
                            Self.mobile_engine_initialize(dbPathPtr, apiUrlPtr, geminiPtr, weatherPtr, googlePtr)
                        }
                    }
                }
            }
        }
        
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Initialize the database
    func initializeDatabase() throws -> OperationResult {
        let result = Self.mobile_engine_initialize_database()
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Dispose the engine
    func dispose() {
        Self.mobile_engine_dispose()
    }
    
    // MARK: - Authentication
    
    /// Authenticate with Google ID token
    func authenticate(googleIdToken: String) throws -> AuthenticationResult {
        let result = googleIdToken.withCString { tokenPtr in
            Self.mobile_engine_authenticate(tokenPtr)
        }
        
        return try parseResult(result, as: AuthenticationResult.self)
    }
    
    /// Get current user
    func getCurrentUser() throws -> UserInfo? {
        let result = Self.mobile_engine_get_current_user()
        return try parseOptionalResult(result, as: UserInfo.self)
    }
    
    // MARK: - Patient Profile
    
    /// Get patient profile
    func getPatientProfile() throws -> PatientInfo? {
        let result = Self.mobile_engine_get_patient_profile()
        return try parseOptionalResult(result, as: PatientInfo.self)
    }
    
    /// Update patient profile
    func updatePatientProfile(_ update: PatientUpdateInfo) throws -> OperationResult {
        let updateJson = try jsonEncoder.encode(update)
        let updateString = String(data: updateJson, encoding: .utf8)!
        
        let result = updateString.withCString { updatePtr in
            Self.mobile_engine_update_patient_profile(updatePtr)
        }
        
        return try parseResult(result, as: OperationResult.self)
    }
    
    // MARK: - Vital Signs
    
    /// Record a vital sign
    func recordVitalSign(_ vitalSign: VitalSignInput) throws -> OperationResult {
        let vitalJson = try jsonEncoder.encode(vitalSign)
        let vitalString = String(data: vitalJson, encoding: .utf8)!
        
        let result = vitalString.withCString { vitalPtr in
            Self.mobile_engine_record_vital_sign(vitalPtr)
        }
        
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Record multiple vital signs
    func recordVitalSigns(_ vitalSigns: [VitalSignInput]) throws -> RecordVitalSignsResult {
        let vitalsJson = try jsonEncoder.encode(vitalSigns)
        let vitalsString = String(data: vitalsJson, encoding: .utf8)!
        
        let result = vitalsString.withCString { vitalsPtr in
            Self.mobile_engine_record_vital_signs(vitalsPtr)
        }
        
        return try parseResult(result, as: RecordVitalSignsResult.self)
    }
    
    /// Get vital signs for date range
    func getVitalSigns(from: Date? = nil, to: Date? = nil) throws -> [VitalSignInfo] {
        let fromString = from?.ISO8601Format()
        let toString = to?.ISO8601Format()
        
        let result = fromString?.withCString { fromPtr in
            toString?.withCString { toPtr in
                Self.mobile_engine_get_vital_signs(fromPtr, toPtr)
            } ?? Self.mobile_engine_get_vital_signs(fromPtr, nil)
        } ?? toString?.withCString { toPtr in
            Self.mobile_engine_get_vital_signs(nil, toPtr)
        } ?? Self.mobile_engine_get_vital_signs(nil, nil)
        
        return try parseResult(result, as: [VitalSignInfo].self)
    }
    
    /// Get vital signs by type
    func getVitalSignsByType(_ type: VitalSignType, from: Date? = nil, to: Date? = nil) throws -> [VitalSignInfo] {
        let fromString = from?.ISO8601Format()
        let toString = to?.ISO8601Format()
        
        let result = fromString?.withCString { fromPtr in
            toString?.withCString { toPtr in
                Self.mobile_engine_get_vital_signs_by_type(Int32(type.rawValue), fromPtr, toPtr)
            } ?? Self.mobile_engine_get_vital_signs_by_type(Int32(type.rawValue), fromPtr, nil)
        } ?? toString?.withCString { toPtr in
            Self.mobile_engine_get_vital_signs_by_type(Int32(type.rawValue), nil, toPtr)
        } ?? Self.mobile_engine_get_vital_signs_by_type(Int32(type.rawValue), nil, nil)
        
        return try parseResult(result, as: [VitalSignInfo].self)
    }
    
    // MARK: - Synchronization
    
    /// Perform full sync
    func performSync() throws -> OperationResult {
        let result = Self.mobile_engine_perform_sync()
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Push local changes
    func pushLocalChanges() throws -> OperationResult {
        let result = Self.mobile_engine_push_local_changes()
        return try parseResult(result, as: OperationResult.self)
    }
    
    // MARK: - Medications
    
    /// Get all medications
    func getMedications() throws -> [MedicationInfo] {
        let result = Self.mobile_engine_get_medications()
        return try parseResult(result, as: [MedicationInfo].self)
    }
    
    /// Add a medication
    func addMedication(_ input: AddMedicationInput) throws -> OperationResult {
        let json = try jsonEncoder.encode(input)
        let jsonString = String(data: json, encoding: .utf8)!
        let result = jsonString.withCString { ptr in
            Self.mobile_engine_add_medication(ptr)
        }
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Discontinue a medication
    func discontinueMedication(_ input: DiscontinueMedicationInput) throws -> OperationResult {
        let json = try jsonEncoder.encode(input)
        let jsonString = String(data: json, encoding: .utf8)!
        let result = jsonString.withCString { ptr in
            Self.mobile_engine_discontinue_medication(ptr)
        }
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Search drugs by name
    func searchDrugs(query: String) throws -> [DrugSearchResult] {
        let result = query.withCString { ptr in
            Self.mobile_engine_search_drugs(ptr)
        }
        return try parseResult(result, as: [DrugSearchResult].self)
    }
    
    /// Check interactions between medications
    func checkInteractions(rxCuis: [String]) throws -> [MedicationInteractionInfo] {
        let json = try jsonEncoder.encode(rxCuis)
        let jsonString = String(data: json, encoding: .utf8)!
        let result = jsonString.withCString { ptr in
            Self.mobile_engine_check_interactions(ptr)
        }
        return try parseResult(result, as: [MedicationInteractionInfo].self)
    }
    
    // MARK: - Environment
    
    /// Get current environment reading for location
    func getEnvironmentReading(latitude: Double, longitude: Double) throws -> EnvironmentReadingInfo {
        let result = Self.mobile_engine_get_environment_reading(latitude, longitude)
        return try parseResult(result, as: EnvironmentReadingInfo.self)
    }
    
    /// Get latest cached environment reading
    func getLatestEnvironmentReading() throws -> EnvironmentReadingInfo? {
        let result = Self.mobile_engine_get_latest_environment_reading()
        return try parseOptionalResult(result, as: EnvironmentReadingInfo.self)
    }
    
    // MARK: - ECG
    
    /// Evaluate an ECG frame for triage
    func evaluateEcgFrame(_ frame: EcgFrameInput) throws -> EcgEvaluationResult {
        let json = try jsonEncoder.encode(frame)
        let jsonString = String(data: json, encoding: .utf8)!
        let result = jsonString.withCString { ptr in
            Self.mobile_engine_evaluate_ecg_frame(ptr)
        }
        return try parseResult(result, as: EcgEvaluationResult.self)
    }
    
    // MARK: - AI Chat
    
    /// Send a chat message to the AI assistant
    func sendChatMessage(_ message: String) throws -> ChatMessageInfo {
        let result = message.withCString { ptr in
            Self.mobile_engine_send_chat_message(ptr)
        }
        return try parseResult(result, as: ChatMessageInfo.self)
    }
    
    /// Get chat history
    func getChatHistory() throws -> [ChatMessageInfo] {
        let result = Self.mobile_engine_get_chat_history()
        return try parseResult(result, as: [ChatMessageInfo].self)
    }
    
    /// Clear chat history
    func clearChatHistory() throws -> OperationResult {
        let result = Self.mobile_engine_clear_chat_history()
        return try parseResult(result, as: OperationResult.self)
    }
    
    // MARK: - Coaching
    
    /// Get AI coaching advice
    func getCoachingAdvice() throws -> CoachingAdviceInfo {
        let result = Self.mobile_engine_get_coaching_advice()
        return try parseResult(result, as: CoachingAdviceInfo.self)
    }
    
    // MARK: - Sleep
    
    /// Record a sleep session
    func recordSleepSession(_ input: SleepSessionInput) throws -> OperationResult {
        let json = try jsonEncoder.encode(input)
        let jsonString = String(data: json, encoding: .utf8)!
        let result = jsonString.withCString { ptr in
            Self.mobile_engine_record_sleep_session(ptr)
        }
        return try parseResult(result, as: OperationResult.self)
    }
    
    /// Get sleep sessions for date range
    func getSleepSessions(from: Date? = nil, to: Date? = nil) throws -> [SleepSessionInfo] {
        let fromString = from?.ISO8601Format()
        let toString = to?.ISO8601Format()
        
        let result = fromString?.withCString { fromPtr in
            toString?.withCString { toPtr in
                Self.mobile_engine_get_sleep_sessions(fromPtr, toPtr)
            } ?? Self.mobile_engine_get_sleep_sessions(fromPtr, nil)
        } ?? toString?.withCString { toPtr in
            Self.mobile_engine_get_sleep_sessions(nil, toPtr)
        } ?? Self.mobile_engine_get_sleep_sessions(nil, nil)
        
        return try parseResult(result, as: [SleepSessionInfo].self)
    }
    
    // MARK: - Medical History & OCR
    
    /// Get medical history entries
    func getMedicalHistory() throws -> [MedicalHistoryEntryInfo] {
        let result = Self.mobile_engine_get_medical_history()
        return try parseResult(result, as: [MedicalHistoryEntryInfo].self)
    }
    
    /// Get OCR scanned documents
    func getOcrDocuments() throws -> [OcrDocumentInfo] {
        let result = Self.mobile_engine_get_ocr_documents()
        return try parseResult(result, as: [OcrDocumentInfo].self)
    }
    
    // MARK: - OCR Text Processing
    
    /// Classify document type from OCR text
    func classifyDocument(_ ocrText: String) -> String {
        ocrText.withCString { ptr in
            guard let result = Self.mobile_engine_classify_document(ptr) else { return "Unknown" }
            defer { Self.mobile_engine_free_string(UnsafeMutablePointer(mutating: result)) }
            return String(cString: result)
        }
    }
    
    /// Extract identity (name + CNP) from OCR text
    func extractIdentity(_ ocrText: String) throws -> DocumentIdentityInfo {
        let result = ocrText.withCString { Self.mobile_engine_extract_identity($0) }
        return try parseResult(result, as: DocumentIdentityInfo.self)
    }
    
    /// Validate extracted identity against current patient
    func validateIdentity(_ ocrText: String) throws -> IdentityValidationInfo {
        let result = ocrText.withCString { Self.mobile_engine_validate_identity($0) }
        return try parseResult(result, as: IdentityValidationInfo.self)
    }
    
    /// Sanitize text (redact PII)
    func sanitizeText(_ ocrText: String) -> String {
        ocrText.withCString { ptr in
            guard let result = Self.mobile_engine_sanitize_text(ptr) else { return ocrText }
            defer { Self.mobile_engine_free_string(UnsafeMutablePointer(mutating: result)) }
            return String(cString: result)
        }
    }
    
    /// Extract structured fields from OCR text
    func extractStructured(_ ocrText: String, documentType: String) throws -> HeuristicExtractionInfo {
        let result = ocrText.withCString { ocrPtr in
            documentType.withCString { docPtr in
                Self.mobile_engine_extract_structured(ocrPtr, docPtr)
            }
        }
        return try parseResult(result, as: HeuristicExtractionInfo.self)
    }
    
    /// Full OCR processing pipeline
    func processFullOcr(_ ocrText: String) throws -> OcrProcessingResult {
        let result = ocrText.withCString { Self.mobile_engine_process_full_ocr($0) }
        return try parseResult(result, as: OcrProcessingResult.self)
    }
    
    /// Save scanned document and extract medical history
    func saveOcrDocument(opaqueInternalName: String, mimeType: String, pageCount: Int, pageTexts: [String]) throws -> OcrDocumentInfo {
        let input = SaveOcrDocumentInput(
            opaqueInternalName: opaqueInternalName,
            mimeType: mimeType,
            pageCount: pageCount,
            pageTexts: pageTexts
        )
        let json = try jsonEncoder.encode(input)
        let jsonStr = String(data: json, encoding: .utf8)!
        let result = jsonStr.withCString { Self.mobile_engine_save_ocr_document($0) }
        return try parseResult(result, as: OcrDocumentInfo.self)
    }
    
    // MARK: - Private Helpers
    
    private func parseResult<T: Codable>(_ cStringPtr: UnsafePointer<CChar>?, as type: T.Type) throws -> T {
        guard let cStringPtr = cStringPtr else {
            throw BridgeError.nullResponse
        }
        
        defer { Self.mobile_engine_free_string(cStringPtr) }
        
        let jsonString = String(cString: cStringPtr)
        guard let jsonData = jsonString.data(using: .utf8) else {
            throw BridgeError.invalidResponse
        }
        
        do {
            return try jsonDecoder.decode(T.self, from: jsonData)
        } catch {
            // Try to parse as error response
            if let errorResponse = try? jsonDecoder.decode(ErrorResponse.self, from: jsonData) {
                throw BridgeError.engineError(errorResponse.error ?? "Unknown error")
            }
            throw BridgeError.decodingFailed(error)
        }
    }
    
    private func parseOptionalResult<T: Codable>(_ cStringPtr: UnsafePointer<CChar>?, as type: T.Type) throws -> T? {
        guard let cStringPtr = cStringPtr else {
            return nil
        }
        
        defer { Self.mobile_engine_free_string(cStringPtr) }
        
        let jsonString = String(cString: cStringPtr)
        if jsonString == "null" || jsonString.isEmpty {
            return nil
        }
        
        guard let jsonData = jsonString.data(using: .utf8) else {
            throw BridgeError.invalidResponse
        }
        
        return try jsonDecoder.decode(T.self, from: jsonData)
    }
}

// MARK: - Bridge Errors

enum BridgeError: LocalizedError {
    case nullResponse
    case invalidResponse
    case engineError(String)
    case decodingFailed(Error)
    
    var errorDescription: String? {
        switch self {
        case .nullResponse:
            return "No response from .NET engine"
        case .invalidResponse:
            return "Invalid response format"
        case .engineError(let message):
            return "Engine error: \(message)"
        case .decodingFailed(let error):
            return "Failed to decode response: \(error.localizedDescription)"
        }
    }
}

// MARK: - Response Models

private struct ErrorResponse: Codable {
    let success: Bool
    let error: String?
}

struct RecordVitalSignsResult: Codable {
    let success: Bool
    let count: Int
    let error: String?
}