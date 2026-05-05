import Foundation

struct AuthenticationResult: Codable {
    let success: Bool
    let errorMessage: String?
    let accessToken: String?
    let user: UserInfo?
    let hasCloudProfile: Bool?
}

