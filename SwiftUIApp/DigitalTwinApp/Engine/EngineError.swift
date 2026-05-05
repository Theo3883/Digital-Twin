import Foundation

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

