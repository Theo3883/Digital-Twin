import Foundation

struct FetchEnvironmentReadingUseCase: Sendable {
    private let repository: EnvironmentRepository

    init(repository: EnvironmentRepository) {
        self.repository = repository
    }

    func callAsFunction(latitude: Double, longitude: Double) async -> EnvironmentReadingInfo? {
        await repository.fetch(latitude: latitude, longitude: longitude)
    }
}

