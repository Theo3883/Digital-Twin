import Foundation

@MainActor
final class EngineDoctorRepository: DoctorRepository {
    private let engine: MobileEngineWrapper

    init(engine: MobileEngineWrapper) {
        self.engine = engine
    }

    func loadAssignedDoctors() async -> [AssignedDoctorInfo] {
        await engine.loadAssignedDoctors()
        return engine.assignedDoctors
    }
}
