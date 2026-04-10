import Foundation

struct UserInfo: Codable, Identifiable {
    let id: UUID
    let email: String
    let role: Int?
    let firstName: String?
    let lastName: String?
    let photoUrl: String?
    let phone: String?
    let address: String?
    let city: String?
    let country: String?
    let dateOfBirth: Date?
    let createdAt: Date?
    let updatedAt: Date?

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

