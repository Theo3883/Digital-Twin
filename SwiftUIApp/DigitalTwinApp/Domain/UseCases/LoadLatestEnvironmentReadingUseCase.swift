import Foundation

struct LoadLatestEnvironmentReadingUseCase: Sendable {
    private let repository: EnvironmentRepository

    init(repository: EnvironmentRepository) {
        self.repository = repository
    }

    func callAsFunction() async -> EnvironmentReadingInfo? {
        await repository.loadLatest()
    }
}

