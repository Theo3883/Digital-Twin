import Foundation

struct AssignedDoctorInfo: Codable, Identifiable {
    let doctorId: UUID
    let fullName: String
    let email: String
    let photoUrl: String?
    let assignedAt: Date?
    let notes: String?

    var id: UUID { doctorId }

    private enum CodingKeys: String, CodingKey {
        case doctorId
        case fullName
        case email
        case photoUrl
        case assignedAt
        case notes
    }

    init(
        doctorId: UUID,
        fullName: String,
        email: String,
        photoUrl: String?,
        assignedAt: Date?,
        notes: String?
    ) {
        self.doctorId = doctorId
        self.fullName = fullName
        self.email = email
        self.photoUrl = photoUrl
        self.assignedAt = assignedAt
        self.notes = notes
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)

        doctorId = try container.decode(UUID.self, forKey: .doctorId)
        fullName = (try container.decodeIfPresent(String.self, forKey: .fullName)) ?? "Unknown Doctor"
        email = (try container.decodeIfPresent(String.self, forKey: .email)) ?? ""
        photoUrl = try container.decodeIfPresent(String.self, forKey: .photoUrl)
        notes = try container.decodeIfPresent(String.self, forKey: .notes)

        assignedAt = try? container.decode(Date.self, forKey: .assignedAt)
    }
}
