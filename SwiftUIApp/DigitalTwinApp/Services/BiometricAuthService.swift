import LocalAuthentication
import Foundation

/// Service for managing biometric authentication (Face ID / Touch ID)
@MainActor
class BiometricAuthService: ObservableObject {
    
    // MARK: - Properties
    
    @Published var isAvailable = false
    @Published var biometricType: LABiometryType = .none
    @Published var isEnabled = false
    @Published var authenticationStatus: AuthenticationStatus = .notConfigured
    
    private let context = LAContext()
    private let keychainService = "com.digitaltwin.mobile.biometric"
    
    // MARK: - Initialization
    
    init() {
        checkBiometricAvailability()
        loadBiometricPreference()
    }
    
    // MARK: - Biometric Availability
    
    /// Check if biometric authentication is available
    func checkBiometricAvailability() {
        var error: NSError?
        
        isAvailable = context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error)
        biometricType = context.biometryType
        
        if let error = error {
            print("[BiometricAuthService] Biometric availability error: \(error.localizedDescription)")
            authenticationStatus = .error(error.localizedDescription)
        } else if isAvailable {
            authenticationStatus = isEnabled ? .configured : .notConfigured
        } else {
            authenticationStatus = .notAvailable
        }
    }
    
    /// Get biometric type display name
    var biometricTypeDisplayName: String {
        switch biometricType {
        case .faceID:
            return "Face ID"
        case .touchID:
            return "Touch ID"
        case .opticID:
            return "Optic ID"
        case .none:
            return "Biometric Authentication"
        @unknown default:
            return "Biometric Authentication"
        }
    }
    
    // MARK: - Authentication
    
    /// Authenticate using biometrics
    func authenticate(reason: String = "Authenticate to access your health data") async -> AuthResult {
        guard isAvailable else {
            return .failure(.notAvailable)
        }
        
        guard isEnabled else {
            return .failure(.notEnabled)
        }
        
        let context = LAContext()
        context.localizedFallbackTitle = "Use Passcode"
        
        do {
            let success = try await context.evaluatePolicy(
                .deviceOwnerAuthenticationWithBiometrics,
                localizedReason: reason
            )
            
            if success {
                authenticationStatus = .authenticated
                return .success
            } else {
                authenticationStatus = .failed
                return .failure(.authenticationFailed)
            }
            
        } catch let error as LAError {
            let authError = mapLAError(error)
            authenticationStatus = .error(authError.localizedDescription)
            return .failure(authError)
            
        } catch {
            authenticationStatus = .error(error.localizedDescription)
            return .failure(.unknown(error.localizedDescription))
        }
    }
    
    /// Authenticate with fallback to device passcode
    func authenticateWithFallback(reason: String = "Authenticate to access your health data") async -> AuthResult {
        guard isAvailable || context.canEvaluatePolicy(.deviceOwnerAuthentication, error: nil) else {
            return .failure(.notAvailable)
        }
        
        let context = LAContext()
        context.localizedFallbackTitle = "Use Passcode"
        
        do {
            let success = try await context.evaluatePolicy(
                .deviceOwnerAuthentication,
                localizedReason: reason
            )
            
            if success {
                authenticationStatus = .authenticated
                return .success
            } else {
                authenticationStatus = .failed
                return .failure(.authenticationFailed)
            }
            
        } catch let error as LAError {
            let authError = mapLAError(error)
            authenticationStatus = .error(authError.localizedDescription)
            return .failure(authError)
            
        } catch {
            authenticationStatus = .error(error.localizedDescription)
            return .failure(.unknown(error.localizedDescription))
        }
    }
    
    // MARK: - Settings Management
    
    /// Enable biometric authentication
    func enableBiometricAuth() async -> Bool {
        guard isAvailable else {
            authenticationStatus = .error("Biometric authentication not available")
            return false
        }
        
        // First authenticate to confirm the user can use biometrics
        let result = await authenticate(reason: "Enable biometric authentication for this app")
        
        switch result {
        case .success:
            isEnabled = true
            authenticationStatus = .configured
            saveBiometricPreference(true)
            
            // Store a marker in keychain to verify biometric setup
            let success = storeBiometricMarker()
            if !success {
                print("[BiometricAuthService] Failed to store biometric marker in keychain")
            }
            
            return true
            
        case .failure(let error):
            print("[BiometricAuthService] Failed to enable biometric auth: \(error.localizedDescription)")
            return false
        }
    }
    
    /// Disable biometric authentication
    func disableBiometricAuth() {
        isEnabled = false
        authenticationStatus = .notConfigured
        saveBiometricPreference(false)
        removeBiometricMarker()
    }
    
    /// Check if biometric authentication is properly configured
    func verifyBiometricSetup() -> Bool {
        guard isAvailable && isEnabled else { return false }
        return verifyBiometricMarker()
    }
    
    // MARK: - Private Helpers
    
    private func mapLAError(_ error: LAError) -> AuthError {
        switch error.code {
        case .authenticationFailed:
            return .authenticationFailed
        case .userCancel:
            return .userCancelled
        case .userFallback:
            return .userFallback
        case .systemCancel:
            return .systemCancelled
        case .passcodeNotSet:
            return .passcodeNotSet
        case .biometryNotAvailable:
            return .notAvailable
        case .biometryNotEnrolled:
            return .notEnrolled
        case .biometryLockout:
            return .biometryLockout
        case .appCancel:
            return .appCancelled
        case .invalidContext:
            return .invalidContext
        case .notInteractive:
            return .notInteractive
        default:
            return .unknown(error.localizedDescription)
        }
    }
    
    // MARK: - Preferences Storage
    
    private func saveBiometricPreference(_ enabled: Bool) {
        UserDefaults.standard.set(enabled, forKey: "biometricAuthEnabled")
    }
    
    private func loadBiometricPreference() {
        isEnabled = UserDefaults.standard.bool(forKey: "biometricAuthEnabled")
        
        if isEnabled && isAvailable {
            authenticationStatus = verifyBiometricSetup() ? .configured : .notConfigured
        }
    }
    
    // MARK: - Keychain Management
    
    private func storeBiometricMarker() -> Bool {
        let data = "biometric_enabled".data(using: .utf8)!
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: "biometric_marker",
            kSecValueData as String: data,
            kSecAttrAccessControl as String: createBiometricAccessControl()
        ]
        
        // Delete existing item first
        SecItemDelete(query as CFDictionary)
        
        let status = SecItemAdd(query as CFDictionary, nil)
        return status == errSecSuccess
    }
    
    private func verifyBiometricMarker() -> Bool {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: "biometric_marker",
            kSecReturnData as String: true
        ]
        
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        
        return status == errSecSuccess
    }
    
    private func removeBiometricMarker() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: keychainService,
            kSecAttrAccount as String: "biometric_marker"
        ]
        
        SecItemDelete(query as CFDictionary)
    }
    
    private func createBiometricAccessControl() -> SecAccessControl {
        var error: Unmanaged<CFError>?
        
        let accessControl = SecAccessControlCreateWithFlags(
            kCFAllocatorDefault,
            kSecAttrAccessibleWhenUnlockedThisDeviceOnly,
            .biometryAny,
            &error
        )
        
        if let error = error {
            print("[BiometricAuthService] Failed to create access control: \(error.takeRetainedValue())")
        }
        
        return accessControl!
    }
}

// MARK: - Authentication Status

enum AuthenticationStatus {
    case notConfigured
    case configured
    case notAvailable
    case authenticated
    case failed
    case error(String)
    
    var displayName: String {
        switch self {
        case .notConfigured:
            return "Not Configured"
        case .configured:
            return "Configured"
        case .notAvailable:
            return "Not Available"
        case .authenticated:
            return "Authenticated"
        case .failed:
            return "Authentication Failed"
        case .error(let message):
            return "Error: \(message)"
        }
    }
}

// MARK: - Authentication Result

enum AuthResult {
    case success
    case failure(AuthError)
}

// MARK: - Authentication Errors

enum AuthError: LocalizedError {
    case notAvailable
    case notEnabled
    case notEnrolled
    case authenticationFailed
    case userCancelled
    case userFallback
    case systemCancelled
    case passcodeNotSet
    case biometryLockout
    case appCancelled
    case invalidContext
    case notInteractive
    case unknown(String)
    
    var errorDescription: String? {
        switch self {
        case .notAvailable:
            return "Biometric authentication is not available on this device"
        case .notEnabled:
            return "Biometric authentication is not enabled for this app"
        case .notEnrolled:
            return "No biometric data is enrolled on this device"
        case .authenticationFailed:
            return "Biometric authentication failed"
        case .userCancelled:
            return "Authentication was cancelled by the user"
        case .userFallback:
            return "User chose to use fallback authentication"
        case .systemCancelled:
            return "Authentication was cancelled by the system"
        case .passcodeNotSet:
            return "Device passcode is not set"
        case .biometryLockout:
            return "Biometric authentication is locked out. Use device passcode to unlock"
        case .appCancelled:
            return "Authentication was cancelled by the app"
        case .invalidContext:
            return "Invalid authentication context"
        case .notInteractive:
            return "Authentication context is not interactive"
        case .unknown(let message):
            return "Unknown authentication error: \(message)"
        }
    }
}

// MARK: - Convenience Extensions

extension BiometricAuthService {
    
    /// Quick check if user should be prompted for authentication
    var shouldPromptForAuthentication: Bool {
        return isAvailable && isEnabled && verifyBiometricSetup()
    }
    
    /// Get appropriate authentication prompt message
    func getAuthenticationPrompt(for action: String) -> String {
        return "Use \(biometricTypeDisplayName) to \(action)"
    }
}