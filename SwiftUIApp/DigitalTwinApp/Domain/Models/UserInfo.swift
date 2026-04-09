import Foundation

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

