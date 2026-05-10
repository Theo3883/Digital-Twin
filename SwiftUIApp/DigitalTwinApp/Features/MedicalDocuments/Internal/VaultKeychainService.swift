import Foundation
import LocalAuthentication
import Security

/// Manages the vault master key in the iOS Keychain.
@MainActor
final class VaultKeychainService: ObservableObject {
    @Published private(set) var keyExists = false

    private let service = "com.digitaltwin.ocrVault"
    private let account = "masterKey"

    private enum StoreMasterKeyResult {
        case stored
        case alreadyExists
        case failed(OSStatus)
    }

    init() {
        keyExists = checkKeyExists()
    }

    func generateAndStoreMasterKey() -> String? {
        if checkKeyExists() {
            keyExists = true
            print("[VaultKeychain] generateAndStoreMasterKey skipped: key already exists")
            return nil
        }

        var keyData = Data(count: 32)
        let status = keyData.withUnsafeMutableBytes { ptr in
            SecRandomCopyBytes(kSecRandomDefault, 32, ptr.baseAddress!)
        }
        guard status == errSecSuccess else {
            print("[VaultKeychain] Failed to generate random master key: \(osStatusDescription(status))")
            return nil
        }

        switch storeMasterKey(keyData) {
        case .stored:
            keyExists = true
            print("[VaultKeychain] Master key generated and stored")
            return keyData.base64EncodedString()
        case .alreadyExists:
            keyExists = true
            print("[VaultKeychain] Master key store skipped: duplicate key item detected")
            return nil
        case .failed(let storeStatus):
            print("[VaultKeychain] Failed to store master key: \(osStatusDescription(storeStatus))")
            return nil
        }
    }

    func retrieveMasterKey(reason: String = "Unlock document vault") async -> String? {
        let context = LAContext()
        context.localizedReason = reason
        // Allow credential reuse for 10 seconds so the biometric auth is reused by SecItemCopyMatching
        context.touchIDAuthenticationAllowableReuseDuration = 10

        var authError: NSError?
        guard context.canEvaluatePolicy(.deviceOwnerAuthentication, error: &authError) else {
            if let authError {
                print("[VaultKeychain] deviceOwnerAuthentication unavailable: \(authError.localizedDescription)")
            }
            return nil
        }

        let authSucceeded = await withCheckedContinuation { continuation in
            context.evaluatePolicy(.deviceOwnerAuthentication, localizedReason: reason) { success, error in
                if let error {
                    print("[VaultKeychain] Authentication callback error: \(error.localizedDescription)")
                }
                continuation.resume(returning: success)
            }
        }

        guard authSucceeded else {
            print("[VaultKeychain] Authentication failed or cancelled")
            return nil
        }

        print("[VaultKeychain] Authentication passed")

        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecUseAuthenticationContext as String: context,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]

        return await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                var result: AnyObject?
                let status = SecItemCopyMatching(query as CFDictionary, &result)
                if status == errSecSuccess, let data = result as? Data {
                    print("[VaultKeychain] Retrieved master key, bytes=\(data.count)")
                    continuation.resume(returning: data.base64EncodedString())
                } else {
                    print("[VaultKeychain] Failed to retrieve master key: \(self.osStatusDescription(status))")
                    continuation.resume(returning: nil)
                }
            }
        }
    }

    func deleteMasterKey() -> Bool {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
        let status = SecItemDelete(query as CFDictionary)
        if status == errSecSuccess || status == errSecItemNotFound {
            keyExists = false
            print("[VaultKeychain] Master key deleted (or already absent)")
            return true
        }
        print("[VaultKeychain] Failed to delete master key: \(osStatusDescription(status))")
        return false
    }

    private func storeMasterKey(_ keyData: Data) -> StoreMasterKeyResult {
        guard let access = SecAccessControlCreateWithFlags(
            nil,
            kSecAttrAccessibleWhenPasscodeSetThisDeviceOnly,
            .userPresence,
            nil
        ) else {
            return .failed(errSecParam)
        }

        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecValueData as String: keyData,
            kSecAttrAccessControl as String: access
        ]

        let status = SecItemAdd(query as CFDictionary, nil)
        if status == errSecSuccess {
            return .stored
        }

        if status == errSecDuplicateItem {
            return .alreadyExists
        }

        return .failed(status)
    }

    private func checkKeyExists() -> Bool {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecUseAuthenticationUI as String: kSecUseAuthenticationUIFail
        ]
        let status = SecItemCopyMatching(query as CFDictionary, nil)
        let exists = status == errSecSuccess ||
            status == errSecInteractionNotAllowed ||
            status == errSecAuthFailed

        print("[VaultKeychain] checkKeyExists status=\(osStatusDescription(status)) exists=\(exists)")
        return exists
    }

    private func osStatusDescription(_ status: OSStatus) -> String {
        if let message = SecCopyErrorMessageString(status, nil) as String? {
            return "\(status) (\(message))"
        }

        return "\(status)"
    }
}
