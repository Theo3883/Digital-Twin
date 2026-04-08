import Foundation

/// Swift bridge to the .NET Mobile Engine
/// Provides Swift-friendly interface to the embedded .NET backend
class DotNetBridge {
    
    // MARK: - C Function Declarations
    
    @_silgen_name("mobile_engine_initialize")
    private static func mobile_engine_initialize(_ databasePath: UnsafePointer<CChar>, _ apiBaseUrl: UnsafePointer<CChar>) -> UnsafePointer<CChar>?
    
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
    func initialize(databasePath: String, apiBaseUrl: String) throws -> OperationResult {
        let result = databasePath.withCString { dbPathPtr in
            apiBaseUrl.withCString { apiUrlPtr in
                Self.mobile_engine_initialize(dbPathPtr, apiUrlPtr)
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