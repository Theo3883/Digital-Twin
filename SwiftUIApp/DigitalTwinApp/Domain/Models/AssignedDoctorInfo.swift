import Foundation

struct AssignedDoctorInfo: Codable, Identifiable {
    let doctorId: UUID
    let fullName: String
    let email: String
    let photoUrl: String?
    let assignedAt: Date
    let notes: String?

    var id: UUID { doctorId }
}
