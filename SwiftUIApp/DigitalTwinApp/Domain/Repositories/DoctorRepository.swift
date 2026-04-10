import Foundation

protocol DoctorRepository: Sendable {
    func loadAssignedDoctors() async -> [AssignedDoctorInfo]
}
