import Foundation

struct GetAssignedDoctorsUseCase: Sendable {
    private let repository: DoctorRepository

    init(repository: DoctorRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> [AssignedDoctorInfo] {
        await repository.loadAssignedDoctors()
    }
}
