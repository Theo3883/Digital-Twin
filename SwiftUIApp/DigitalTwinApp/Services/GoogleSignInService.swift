import Foundation
import GoogleSignIn

/// Manages Google Sign-In for iOS using the Google Sign-In SDK.
@MainActor
final class GoogleSignInService: ObservableObject {
    
    @Published var isSignedIn = false
    @Published var errorMessage: String?
    
    /// The Google OAuth client ID from the Google Cloud console plist.
    private let clientID: String
    
    init() {
        self.clientID = Bundle.main.infoDictionary?["GOOGLE_OAUTH_CLIENT_ID"] as? String ?? ""
    }
    
    /// Perform Google Sign-In and return the ID token for backend verification.
    func signIn() async throws -> String {
        guard !clientID.isEmpty else {
            throw GoogleSignInError.missingClientID
        }
        
        let config = GIDConfiguration(clientID: clientID)
        GIDSignIn.sharedInstance.configuration = config
        
        guard let windowScene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
              let rootViewController = windowScene.windows.first?.rootViewController else {
            throw GoogleSignInError.noRootViewController
        }
        
        let result = try await GIDSignIn.sharedInstance.signIn(withPresenting: rootViewController)
        
        guard let idToken = result.user.idToken?.tokenString else {
            throw GoogleSignInError.noIDToken
        }
        
        isSignedIn = true
        return idToken
    }
    
    /// Restore a previous sign-in session if available.
    func restorePreviousSignIn() async -> String? {
        do {
            let user = try await GIDSignIn.sharedInstance.restorePreviousSignIn()
            if let idToken = user.idToken?.tokenString {
                isSignedIn = true
                return idToken
            }
        } catch {
            // No previous session — user needs to sign in again
        }
        return nil
    }
    
    /// Sign out of the current Google session.
    func signOut() {
        GIDSignIn.sharedInstance.signOut()
        isSignedIn = false
    }
    
    /// Handle the OAuth redirect URL callback.
    static func handleURL(_ url: URL) -> Bool {
        GIDSignIn.sharedInstance.handle(url)
    }
}

enum GoogleSignInError: LocalizedError {
    case missingClientID
    case noRootViewController
    case noIDToken
    
    var errorDescription: String? {
        switch self {
        case .missingClientID:
            "Google OAuth client ID is not configured."
        case .noRootViewController:
            "Unable to find a root view controller for sign-in."
        case .noIDToken:
            "Google Sign-In succeeded but no ID token was returned."
        }
    }
}
